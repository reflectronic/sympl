using System;
using System.Diagnostics;
using Xunit;

namespace Sympl.Compiler.Tests
{
    public class IndexingTests : ScriptEngineTests
    {
        public IndexingTests()
        {
            Console.WriteLine("Waiting 10 s for debuger... PID " + Environment.ProcessId);
            System.Threading.Thread.Sleep(20_000);
            Console.WriteLine("IM GONE...");

            Execute("(import system)");
        }

        // ;;; Tests for indexing
        // ;;; create an array containing 3 strings
        // (set a (System.Array.CreateInstance System.String 3))
        // (set (elt a 0) "hi")
        // (set (elt a 1) "hello")
        // (set (elt a 2) "world")
        // (print (elt a 0))
        // (print (elt a 1))
        // (print (elt a 2))
        [Fact]
        public void ArrayIndexing()
        {
            Execute("(set a (System.Array.CreateInstance System.String 3))");
            Execute("(set (elt a 0) \"hi\")");
            Execute("(set (elt a 1) \"hello\")");
            Execute("(set (elt a 2) \"world\")");

            Assert.Equal("hi", Execute<string>("(elt a 0)"));
            Assert.Equal("hello", Execute<string>("(elt a 1)"));
            Assert.Equal("world", Execute<string>("(elt a 2)"));
        }


        // (set sb (new System.Text.StringBuilder "hello world!"))
        // (print (elt sb 6)) ; print the 6th char in the string, which is 'w'
        [Fact]
        public void StringBuilderIndexing()
        {
            Execute("(set sb (new System.Text.StringBuilder \"hello world!\"))");
            Assert.Equal('w', Execute<char>("(elt sb 6)"));
        }

        // ;;; create generic list of int (which requires array of types to start)
        // (set types (System.Array.CreateInstance System.Type 1))
        // (set (elt types 0) System.Int32)
        // (set l (new(System.Collections.Generic.List`1.MakeGenericType types)))

        // ;;; Add an element with Add method
        // (l.Add 100)
        // (print (elt l 0))
        // (set (elt l 0) 200)
        // (print (elt l 0))
        [Fact]
        public void GenericListIndexing()
        {
            Execute("(set types (System.Array.CreateInstance System.Type 1))");
            Execute("(set (elt types 0) System.Int32)");
            Execute("(set l (new(System.Collections.Generic.List`1.MakeGenericType types)))");

            Execute("(l.Add 100)");
            Assert.Equal(100, Execute<int>("(elt l 0)"));
            Execute("(set (elt l 0) 200)");
            Assert.Equal(200, Execute<int>("(elt l 0)"));
        }

        // ;;; tests for Dictionary<string, int>
        // (set types (System.Array.CreateInstance System.Type 2))
        // (set (elt types 0) System.String)
        // (set (elt types 1) System.Int32)
        // (set dict (new(System.Collections.Generic.Dictionary`2.MakeGenericType types)))

        // ;;; Add elements using indexers
        // (set (elt dict "a") 3)
        // (print (elt dict "a"))    ;; print 3
        // (set (elt dict "b") 5)
        // (print (elt dict "b"))    ;; print 5
        [Fact]
        public void GenericDictionaryIndexing()
        {
            Execute("(set types (System.Array.CreateInstance System.Type 2))");
            Execute("(set (elt types 0) System.String)");
            Execute("(set (elt types 1) System.Int32)");
            Execute("(set dict (new(System.Collections.Generic.Dictionary`2.MakeGenericType types)))");

            Execute("(set (elt dict \"a\") 3)");
            Assert.Equal(3, Execute<int>("(elt dict \"a\")"));
            Execute("(set (elt dict \"b\") 5)");
            Assert.Equal(5, Execute<int>("(elt dict \"b\")"));
        }


        // ;;; Test indexing on list literals
        // (set l '(1 2 3))
        // (print (elt l 0))
        // (print (elt l 1))
        // (print (elt l 2))

        // (set (elt l 1) 100)
        // (print (elt l 1))
        [Fact]
        public void ListLiteralIndexing()
        {
            Execute("(set l '(1 2 3))");
            Assert.Equal(1, Execute<int>("(elt l 0)"));
            Assert.Equal(2, Execute<int>("(elt l 1)"));
            Assert.Equal(3, Execute<int>("(elt l 2)"));

            Execute("(set (elt l 1) 100)");
            Assert.Equal(100, Execute<int>("(elt l 1)"));
        }

        // ;;; test multidimentional array
        // ;;; set the dimension lengths of the 2d array int[3][2]

        // (set lengths (System.Array.CreateInstance System.Int32 2))
        // (set (elt lengths 0) 3)
        // (set (elt lengths 1) 2)

        // (set a (System.Array.CreateInstance System.Int32 lengths))
        // (set (elt a 2 0) 11)
        // (print (elt a 2 0)) ; print 11
        // (print (elt a 0 1)) ; print 0, which the default value for uninitialized elements
        [Fact]
        public void MultidimensionalArrayIndexing()
        {
            Execute("(set lengths (System.Array.CreateInstance System.Int32 2))");
            Execute("(set (elt lengths 0) 3)");
            Execute("(set (elt lengths 1) 2)");

            Execute("(set a (System.Array.CreateInstance System.Int32 lengths))");
            Execute("(set (elt a 2 0) 11)");
            Assert.Equal(11, Execute<int>("(elt a 2 0)"));
            Assert.Equal(0, Execute<int>("(elt a 0 1)"));
        }
    }
}
