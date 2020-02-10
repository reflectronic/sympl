using System;

namespace Sympl.Runtime
{
    public class Symbol
    {
        internal Symbol(String name)
        {
            Name = name;
        }

        /// <devdoc>
        /// Need ToString when Sympl program passing <see cref="Symbol" /> to Console.WriteLine. Otherwise, it
        /// prints as internal IPy constructed type.
        /// </devdoc>
        public override String ToString() => Name;

        // C# forces property set and assignments to return void, so need to code gen explicit
        // value return.
        public String Name { get; set; }

        public Object? Value { get; set; }

        public Cons? PList { get; set; }
    }
}