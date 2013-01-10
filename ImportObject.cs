using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;

namespace TechTalk.SpecFlow.ObjectConversion
{
    public static class ImportObjectExtensions
    {
        /// <summary>
        /// Creates a conversion object for the given set of properties
        /// </summary>
        /// <typeparam name="TEntity">the type of the object to create</typeparam>
        public static IImportObject<TEntity> AsImportObject<TEntity>(this IDictionary<string, string> objectData)
        {
            return new ImportObject<TEntity>(objectData.ToDictionary(k => k.Key, v => v.Value));
        }

        /// <summary>
        /// Specifies the aliases in the import data for the given property of the entity. There can be more than one alias for any property, but not vice versa.
        /// </summary>
        public static IConfiguredImportObject<TEntity> WithPropertyAlias<TEntity, TProperty>(this IConfiguredImportObject<TEntity> importObject, Expression<Func<TEntity, TProperty>> selector, params string[] aliases)
        {
            foreach (var alias in aliases)
                importObject.WithPropertyAlias(selector, alias);
            return importObject;
        }

        /// <summary>
        /// Shorthand to specify required fields that the import data is verified to contain in a type-safely way
        /// </summary>
        public static IConfiguredImportObject<TEntity> WithRequiredField<TEntity, TProperty>(this IConfiguredImportObject<TEntity> importObject, params Expression<Func<TEntity, TProperty>>[] propertySelectors)
        {
            return importObject.WithRequiredField(propertySelectors.Select(ps => ps.GetPropertyName()).ToArray());
        }

        /// <summary>
        /// Specifies the default value to use for a property if the import data wouldn't specify otherwise
        /// </summary>
        public static IConfiguredImportObject<TEntity> WithDefaultValue<TEntity, TProperty>(this IConfiguredImportObject<TEntity> importObject, Expression<Func<TEntity, TProperty>> selector, TProperty defaultValue)
        {
            return importObject.WithDefaultValue(selector, () => defaultValue);
        }

        /// <summary>
        /// Returns a converter function for the given type. This method is the default converter provider.
        /// </summary>
        public static Func<string, object> GetDefaultConverter(Type type)
        {
            if (type == typeof(string))
                return s => s;

            if (type.IsGenericType && type.Name == "Nullable`1")
                return val =>
                {
                    if (string.IsNullOrEmpty(val)) return null;
                    else return GetDefaultConverter(type.GetGenericArguments()[0])(val);
                };

            if (type.IsEnum)
                return value => Enum.Parse(type, value);

            return value => TypeDescriptor.GetConverter(type).ConvertFromString(value);
        }
    }

    public static class ExpressionExtensions
    {
        /// <summary>
        /// Returns the property name from a property selector expression
        /// </summary>
        public static string GetPropertyName<TEntity, TValue>(this Expression<Func<TEntity, TValue>> property)
        {
            var exp = (LambdaExpression)property;

            if (exp.Body.NodeType == ExpressionType.Parameter)
                return "item";

            var mExp = (exp.Body.NodeType == ExpressionType.MemberAccess) ?
                (MemberExpression)exp.Body :
                (MemberExpression)((UnaryExpression)exp.Body).Operand;
            return mExp.Member.Name;
        }
    }

    public interface IImportObject<TEntity>
    {
        /// <summary>
        /// Allows customizing the object conversion
        /// </summary>
        IConfiguredImportObject<TEntity> WithConfiguration();
        /// <summary>
        /// Creates the object
        /// </summary>
        TEntity CreateObject();
    }

    public interface IConfiguredImportObject<TEntity> : IImportObject<TEntity>
    {
        /// <summary>
        /// Defines the object factory to use when creating a new entity
        /// </summary>
        IConfiguredImportObject<TEntity> WithObjectFactory(Func<TEntity> factory);
        /// <summary>
        /// Checks if the import data contains the specified fields
        /// </summary>
        IConfiguredImportObject<TEntity> WithRequiredField(params string[] fieldNames);
        /// <summary>
        /// Specifies the fields of the import data the algorithm should skip
        /// </summary>
        IConfiguredImportObject<TEntity> WithSkippedField(params string[] fieldNames);
        /// <summary>
        /// Specifies an alias in the import data for the given property of the entity. There can be more than one alias for any property, but not vice versa.
        /// </summary>
        IConfiguredImportObject<TEntity> WithPropertyAlias<TProperty>(Expression<Func<TEntity, TProperty>> selector, string alias);
        /// <summary>
        /// Specifies the converter to use for the given import data field (converter priority: table field, object property, target value, default)
        /// </summary>
        IConfiguredImportObject<TEntity> WithFieldValueConverter<TProperty>(string name, Func<string, TProperty> converter);
        /// <summary>
        /// Specifies the converter to use for the given property of the entity (converter priority: table field, object property, target value, default)
        /// </summary>
        IConfiguredImportObject<TEntity> WithPropertyValueConverter<TProperty>(Expression<Func<TEntity, TProperty>> selector, Func<string, TProperty> converter);
        /// <summary>
        /// Specifies a conversion method to use when converting to the given value (converter priority: table field, object property, target value, default)
        /// </summary>
        IConfiguredImportObject<TEntity> WithValueConverter<TValue>(Func<string, TValue> converter);
        /// <summary>
        /// Specifies the provider method to use to obtain the default value converter for any given type (converter priority: table field, object property, target value, default)
        /// </summary>
        IConfiguredImportObject<TEntity> WithDefaultConverter(Func<Type, Func<string, object>> converterProvider);
        /// <summary>
        /// Specifies the default value to use for a property if the import data wouldn't specify otherwise
        /// </summary>
        IConfiguredImportObject<TEntity> WithDefaultValue<TProperty>(Expression<Func<TEntity, TProperty>> selector, Func<TProperty> valueProvider);
    }

    internal sealed class ImportObject<TEntity> : IConfiguredImportObject<TEntity>
    {
        private readonly IDictionary<string, string> objectData;
        public ImportObject(IDictionary<string, string> objectData)
        {
            this.objectData = objectData;
        }

        public IConfiguredImportObject<TEntity> WithConfiguration()
        {
            return this;
        }

        public TEntity CreateObject()
        {
            var entity = objectFactory();
            foreach (var name in objectData.Keys.Except(skippedFields))
            {
                string propertyName;
                if (!propertyAliases.TryGetValue(name, out propertyName))
                    propertyName = name;

                var property = typeof(TEntity).GetProperty(propertyName);
                if (property == null)
                    throw new InvalidOperationException(string.Format("Invalid property name '{0}'", propertyName));

                Func<string, object> converter = null;
                if (converter == null) fieldValueConverters.TryGetValue(name, out converter);
                if (converter == null) propertyValueConverters.TryGetValue(propertyName, out converter);
                if (converter == null) valueConverters.TryGetValue(property.PropertyType, out converter);
                if (converter == null) converter = defaultConverterProvider(property.PropertyType);

                property.SetValue(entity, converter(objectData[name]), null);
            }
            foreach (var entry in defaultValueProviders)
            {
                var fieldNames = propertyAliases.Where(pa => pa.Value == entry.Key).Select(pa => pa.Key).Concat(new[] { entry.Key });
                if (!objectData.Keys.Any(k => fieldNames.Contains(k)))
                {
                    var property = typeof(TEntity).GetProperty(entry.Key);
                    property.SetValue(entity, entry.Value(), null);
                }
            }
            return entity;
        }

        public IConfiguredImportObject<TEntity> WithRequiredField(params string[] fieldNames)
        {
            foreach (var name in fieldNames)
            {
                if (!objectData.ContainsKey(name))
                    new InvalidOperationException(string.Format("The field '{0}' is required", name));
            }
            return this;
        }

        private Func<TEntity> objectFactory = () => Activator.CreateInstance<TEntity>();
        public IConfiguredImportObject<TEntity> WithObjectFactory(Func<TEntity> factory)
        {
            objectFactory = factory;
            return this;
        }

        private readonly List<string> skippedFields = new List<string>();
        public IConfiguredImportObject<TEntity> WithSkippedField(params string[] fieldNames)
        {
            foreach (var name in fieldNames)
                skippedFields.Add(name);
            return this;
        }

        private readonly Dictionary<string, string> propertyAliases = new Dictionary<string, string>();
        public IConfiguredImportObject<TEntity> WithPropertyAlias<TProperty>(Expression<Func<TEntity, TProperty>> selector, string alias)
        {
            propertyAliases.Add(alias, selector.GetPropertyName());
            return this;
        }

        private readonly Dictionary<string, Func<string, object>> propertyValueConverters = new Dictionary<string, Func<string, object>>();
        public IConfiguredImportObject<TEntity> WithPropertyValueConverter<TProperty>(Expression<Func<TEntity, TProperty>> selector, Func<string, TProperty> converter)
        {
            propertyValueConverters.Add(selector.GetPropertyName(), v => converter(v));
            return this;
        }

        private readonly Dictionary<string, Func<string, object>> fieldValueConverters = new Dictionary<string, Func<string, object>>();
        public IConfiguredImportObject<TEntity> WithFieldValueConverter<TProperty>(string name, Func<string, TProperty> converter)
        {
            fieldValueConverters.Add(name, v => converter(v));
            return this;
        }

        private readonly Dictionary<Type, Func<string, object>> valueConverters = new Dictionary<Type, Func<string, object>>();
        public IConfiguredImportObject<TEntity> WithValueConverter<TValue>(Func<string, TValue> converter)
        {
            valueConverters.Add(typeof(TValue), v => converter(v));
            return this;
        }

        public Func<Type, Func<string, object>> defaultConverterProvider = ImportObjectExtensions.GetDefaultConverter;
        public IConfiguredImportObject<TEntity> WithDefaultConverter(Func<Type, Func<string, object>> converterProvider)
        {
            defaultConverterProvider = converterProvider;
            return this;
        }

        private readonly Dictionary<string, Func<object>> defaultValueProviders = new Dictionary<string, Func<object>>();
        public IConfiguredImportObject<TEntity> WithDefaultValue<TProperty>(Expression<Func<TEntity, TProperty>> selector, Func<TProperty> valueProvider)
        {
            defaultValueProviders.Add(selector.GetPropertyName(), () => valueProvider());
            return this;
        }
    }
}
