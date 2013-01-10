using System;
using System.Collections.Generic;
using TechTalk.SpecFlow.ObjectConversion;

namespace testsample
{
    class Program
    {
        static void Main(string[] args)
        {
            var obj1 = SimpleConversion();
            CheckSimpleConversion(obj1);

            var obj2 = ConfiguredConversion();
            CheckConfiguredConversion(obj2);
        }

        private static ix SimpleConversion()
        {
            var tableRow = new Dictionary<string, string>
            {
                { "a", "x" },
                { "ax", "" },
                { "b", "12.5" },
                { "bx", "" },
                { "c", "5" },
                { "cx", "" },
                { "d", "true" },
                { "dx", "" },
            };
            return tableRow.AsImportObject<ix>().CreateObject();
        }

        private static void CheckSimpleConversion(ix obj)
        {
            if (obj.a != boo.x) throw new Exception("obj.a != boo.x");
            if (obj.ax != null) throw new Exception("obj.ax != null");
            if (obj.b != 12.5) throw new Exception("obj.b != 12.5");
            if (obj.bx != null) throw new Exception("obj.bx != null");
            if (obj.c != 5) throw new Exception("obj.c != 5");
            if (obj.cx != null) throw new Exception("obj.cx != null");
            if (obj.d != true) throw new Exception("obj.d != true");
            if (obj.dx != null) throw new Exception("obj.cd != null");
        }

        private static ix ConfiguredConversion()
        {
            var tableRow = new Dictionary<string, string>
            {
                { "a", "x" },
                { "au", "" },
                { "av", "s" },
                { "b", "12.5" },
                { "bu", "" },
                { "bv", "foo" },
                { "c", "5" },
                { "cu", "" },
                { "cv", "bar" },
                { "d", "true" },
                { "du", "" },
                { "dv", "maybe" },
                { "e", "bar" },
                { "eu", "n/a" },
                { "ev", "foo" },
            };
            return tableRow.AsImportObject<ix>()
                .WithConfiguration()
                    .WithObjectFactory(() => new ix())
                    .WithDefaultConverter(t => v => Convert(v, t))
                    .WithRequiredField("a", "b", "c", "d")
                    .WithSkippedField("av", "bv", "cv", "dv")
                    .WithPropertyAlias(x => x.ax, "au", "av")
                    .WithPropertyAlias(x => x.bx, "bu", "bv")
                    .WithPropertyAlias(x => x.cx, "cu", "cv")
                    .WithPropertyAlias(x => x.dx, "du", "dv")
                    .WithPropertyAlias(x => x.ex, "eu").WithPropertyAlias(x => x.ez, "ev")
                    .WithValueConverter(str => new foo { a = str }) //matches: e, eu, ev
                    .WithFieldValueConverter("eu", str => ((str == "n/a") ? null : new foo { a = str })) //matches: eu
                    .WithPropertyValueConverter(x => x.ez, str => new foo { a = str.Replace("foo", "...") }) //matches: ev
                    .WithDefaultValue(x => x.az, () => boo.y)
                    .WithDefaultValue(x => x.bz, () => 52.725)
                    .WithDefaultValue(x => x.cz, 24)
                    .WithDefaultValue(x => x.dz, false)
                    .WithDefaultValue(x => x.f, () => new foo { a = "err" })
                .CreateObject()
            ;
        }

        private static void CheckConfiguredConversion(ix obj)
        {
            if (obj.a != boo.x) throw new Exception("obj.a != boo.x");
            if (obj.ax != null) throw new Exception("obj.ax != null");
            if (obj.az != boo.y) throw new Exception("obj.ax != boo.y");
            if (obj.b != 12.5) throw new Exception("obj.b != 12.5");
            if (obj.bx != null) throw new Exception("obj.bx != null");
            if (obj.bz != 52.725) throw new Exception("obj.bx != 52,725");
            if (obj.c != 5) throw new Exception("obj.c != 5");
            if (obj.cx != null) throw new Exception("obj.cx != null");
            if (obj.cz != 24) throw new Exception("obj.cx != 24");
            if (obj.d != true) throw new Exception("obj.d != true");
            if (obj.dx != null) throw new Exception("obj.cd != null");
            if (obj.dz != false) throw new Exception("obj.cd != false");
            if (obj.e == null || obj.e.a != "bar") throw new Exception("obj.e == null || obj.e.a != \"bar\"");
            if (obj.ex != null) throw new Exception("obj.ex != null");
            if (obj.ez == null || obj.ez.a != "...") throw new Exception("obj.ez == null || obj.ez.a != \"...\"");
            if (obj.f == null || obj.f.a != "err") throw new Exception("obj.f == null || obj.f.a != \"err\"");
        }

        private static object Convert(string value, Type type)
        {
            return ImportObjectExtensions.GetDefaultConverter(type)(value);
        }
    }

    class ix
    {
        public boo? a { get; set; }
        public boo? ax { get; set; }
        public boo? az { get; set; }

        public double? b { get; set; }
        public double? bx { get; set; }
        public double? bz { get; set; }

        public int? c { get; set; }
        public int? cx { get; set; }
        public int? cz { get; set; }

        public bool? d { get; set; }
        public bool? dx { get; set; }
        public bool? dz { get; set; }

        public foo e { get; set; }
        public foo ex { get; set; }
        public foo ez { get; set; }

        public foo f { get; set; }
    }

    class foo
    {
        public string a { get; set; }
    }

    enum boo
    {
        x,
        y
    }
}
