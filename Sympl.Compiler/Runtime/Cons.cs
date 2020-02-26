using System;

namespace Sympl.Runtime
{
    public class Cons
    {
        public Cons(Object? first, Object? rest)
        {
            First = first;
            Rest = rest;
        }

        // NOTE: does not handle circular references!
        public override String ToString()
        {
            Cons? head = this;
            var res = "(";

            while (head is { })
            {
                res += head.First?.ToString() ?? "nil";
                switch (head.Rest)
                {
                    case null:
                        head = null;
                        break;
                    case Cons rest:
                        head = rest;
                        res += " ";
                        break;
                    default:
                        res = $"{res} . {head.Rest}";
                        head = null;
                        break;
                }
            }

            return $"{res})";
        }

        // C# forces property set and assignments to return void, so need to code gen explicit
        // value return.
        public Object? First { get; set; }

        public Object? Rest { get; set; }

        /// <summary>
        /// Runtime helper method.
        /// </summary>
        public static Cons? _List(params Object?[] elements)
        {
            if (elements.Length == 0) 
                return null;
            
            var head = new Cons(elements[0], null);
            var tail = head;
            
            foreach (var elt in RuntimeHelpers.RemoveFirstElt(elements))
            {
                var cons = new Cons(elt, null);
                tail.Rest = cons;
                tail = cons;
            }

            return head;
        }
    }
}