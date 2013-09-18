﻿using System.Collections.Generic;
using System.Linq;
using CppSharp.AST;

namespace CppSharp.Passes
{
    public class MultipleInheritancePass : TranslationUnitPass
    {
        /// <summary>
        /// Collects all interfaces in a unit to be added at the end because the unit cannot be changed while it's being iterated though.
        /// </summary>
        private readonly Dictionary<Class, Class> interfaces = new Dictionary<Class, Class>();

        public override bool VisitTranslationUnit(TranslationUnit unit)
        {
            bool result = base.VisitTranslationUnit(unit);
            foreach (var @interface in interfaces)
                @interface.Key.Namespace.Classes.Add(@interface.Value);
            interfaces.Clear();
            return result;
        }

        public override bool VisitClassDecl(Class @class)
        {
            for (int i = 1; i < @class.Bases.Count; i++)
            {
                var @base = @class.Bases[i].Class;
                if (@base.IsInterface) continue;

                if (@base.CompleteDeclaration != null)
                    @base = (Class) @base.CompleteDeclaration;
                var name = "I" + @base.Name;
                var @interface = (interfaces.ContainsKey(@base)
                    ? interfaces[@base]
                    : @base.Namespace.Classes.FirstOrDefault(c => c.Name == name)) ??
                                 GetNewInterface(@class, name, @base);
                @class.Bases[i] = new BaseClassSpecifier { Type = new TagType(@interface) };
            }
            return base.VisitClassDecl(@class);
        }

        private Class GetNewInterface(Class @class, string name, Class @base)
        {
            var @interface = new Class
                {
                    Name = name,
                    Namespace = @base.Namespace,
                    Access = @base.Access,
                    IsInterface = true
                };
            @interface.Methods.AddRange(@base.Methods.Where(
                m => !m.IsConstructor && !m.IsDestructor && !m.IsStatic && !m.Ignore));
            foreach (var method in @interface.Methods)
            {
                var impl = new Method(method)
                    {
                        Namespace = @class,
                        IsVirtual = false,
                        IsOverride = false
                    };
                var rootBaseMethod = @class.GetRootBaseMethod(method);
                if (rootBaseMethod != null && !rootBaseMethod.Ignore)
                    impl.Name = @interface.Name + "." + impl.Name;
                @class.Methods.Add(impl);
            }
            @interface.Properties.AddRange(@base.Properties.Where(p => !p.Ignore));
            @class.Properties.AddRange(
                from property in @interface.Properties
                select new Property(property) { Namespace = @class });
            @interface.Events.AddRange(@base.Events);
            if (@base.Bases.All(b => b.Class != @interface))
                @base.Bases.Add(new BaseClassSpecifier { Type = new TagType(@interface) });
            interfaces.Add(@base, @interface);
            return @interface;
        }
    }
}
