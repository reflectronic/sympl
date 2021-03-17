using System;
using Xunit;

namespace Sympl.Compiler.Tests
{
    public class OperationsTests : ScriptEngineTests
    {
        public OperationsTests()
        {
            Execute<object>("(set str \"hello\")");
            Execute<object>("(set len str.length)");
        }

        // (print (+ len 2)) ; 7
        // (print (- len 2)) ; 3
        // (print (* len 2)) ; 10
        // (print (/ len 2)) ; 2
        // (print (/ (* (+ len len)     
        //              (- len 100))
        //           5)) ; -190
        [Fact]
        public void ArithmeticOperators()
        {
            Assert.Equal(5, Execute<int>("len"));
            Assert.Equal(7, Execute<int>("(+ len 2)"));
            Assert.Equal(3, Execute<int>("(- len 2)"));
            Assert.Equal(10, Execute<int>("(* len 2)"));


            Assert.Equal(2, Execute<int>("(/ len 2)"));
            Assert.Equal(-190, Execute<int>(@"(/ (* (+ len len)
                                                    (- len 100))    
                                                 5)"));
        }

        // (print (= len 2)) ; false
        // (print (= len 5)) ; true
        // (print (!= len 2)) ; true
        // (print (!= 5 len)) ; false
        // (print (> len 2)) ; true
        // (print (> len 8)) ; false
        // (print (< len 2)) ; false
        // (print (< len 8)) ; true
        [Fact]
        public void ComparisonOperators()
        {
            Assert.False(Execute<bool>("(= len 2)"));
            Assert.True(Execute<bool>("(= len 5)"));

            Assert.True(Execute<bool>("(!= len 2)"));
            Assert.False(Execute<bool>("(!= 5 len)"));

            Assert.True(Execute<bool>("(> len 2)"));
            Assert.False(Execute<bool>("(> len 8)"));
            Assert.False(Execute<bool>("(< len 2)"));
            Assert.True(Execute<bool>("(< len 8)"));
        }

        // (print (and 5 3)) ; 3
        // (print (and false 3)) ; false
        // (print (and nil 3)) ; false
        // (print (or 5 3))  ; 5
        // (print (or false 3)) ; 3
        // (print (or nil 3))  ; 3
        // (print (or nil nil)) ; false
        // (print (or nil false)) ; false
        // (print (or false false)) ; false
        // (print (and (> len 2) 
        //             (< len 8))) ; true
        // (print (and (> len 6) 
        //             (< len 8))) ; false
        // (print (or (< len 2) 
        //            (= len 5))) ; true
        // (print (or (< len 2) 
        //            (> len 8))) ; false
        [Fact]
        public void BinaryBooleanOperators()
        {
            Assert.Equal(3, Execute<int>("(and 5 3)"));
            Assert.False(Execute<bool>("(and false 3)"));
            Assert.False(Execute<bool>("(and nil 3)"));
            Assert.False(Execute<bool>("(and nil 3)"));
            Assert.Equal(5, Execute<int>("(or 5 3)"));
            Assert.Equal(3, Execute<int>("(or false 3)"));
            Assert.False(Execute<bool>("(or nil nil)"));
            Assert.False(Execute<bool>("(or nil false)"));
            Assert.False(Execute<bool>("(or false false)"));
            Assert.True(Execute<bool>(@"(and (> len 2)
                                              (< len 8))"));
            Assert.False(Execute<bool>(@"(and (> len 6)
                                              (< len 8))"));
            Assert.True(Execute<bool>(@"(or (< len 2)
                                              (= len 5))"));
            Assert.False(Execute<bool>(@"(or (< len 2)
                                              (> len 8))"));
        }

        // (print (not true))  ; false
        // (print (not false)) ; true
        // (print (not nil))   ; true
        // (print (not 0))     ; false
        // (print (not ""))    ; false
        [Fact]
        public void UnaryBooleanOperators()
        {
            Assert.False(Execute<bool>("(not true)"));
            Assert.True(Execute<bool>("(not false)"));
            Assert.True(Execute<bool>("(not nil)"));
            Assert.False(Execute<bool>("(not 0)"));
            Assert.False(Execute<bool>("(not \"\")"));
        }
    }
}
