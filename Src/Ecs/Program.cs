﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Loyc.Essentials;
using Loyc.Collections;
using Loyc.CompilerCore;
using System.Diagnostics;

namespace ecs
{
	class Program
	{
		static void Main(string[] args)
		{
			//if (args.Contains("--genparser"))
			//{
				PrintParser();
			//}
		}

		private static void PrintTesParsers()
		{
			
		}

		private static void PrintParser()
		{
			// Our goal: describe a parser with objects and use it to generate C# source code
			// Notes: LoycPG operators
			// ' ' | '\t'               -- Alt operator (ordered by priority)
			// '_' || LETTER_CHAR       -- Not sure if there is any difference with '|'
			// ESC_SEQ / ~NEWLINE_CHAR  -- Alt operator with warning of ambiguity between alts suppressed
			// '\u0000'..'\u001F'       -- Character range
			// ~WS_CHAR                 -- Inverted character set (applies to single terminals)
			// '/' '*'                  -- Two things in a row
			// "/*"                     -- Equivalent to '/' '*'
			// ('+'|'-')?               -- Zero or one
			// WS_CHAR+                 -- One or more
			// WS_CHAR*                 -- Zero or more
			// nongreedy(.)*            -- Zero or more, exiting loop takes priority over staying
			// nongreedy(.)+            -- One or more, exiting loop takes priority over staying
			// nongreedy(FOO)?          -- Zero or one, zero preferred
			// greedy(...)*             -- Suppress warning about ambiguity between alt and exit
			// &('\\' '\\')             -- Syntactic predicate: sequence matches here, consumes no input
			// &!('\\' '\\')            -- Syntactic predicate: sequence does not match here
			// &{ ...C# expression... } -- Check whether condition is true, consumes no input
			// { ...C# expression... }? -- ANTLR syntax for the same thing, a "validating semantic predicate"
			// '"' => DQ_STRING         -- Gated predicate, can be used to simplify prediction or to resolve ambiguity
			// {...}? => DQ_STRING      -- Called a "gated semantic predicate" in ANTLR; just combines a gate with a C# expr
			// { ...C# statements... }  -- Once input prior to this point is confirmed, run this code. Low precedence.
			// A(C# expression)         -- Call a rule with arguments
			// 
			// Precedence:
			// 1. Unary *, +, ?, &, &!, greedy, nongreedy
			// 2. Juxtaposition
			// 3. Binary =>
			// 4. Binary ||, |, /
			//
			// How would this work as a EC# DSL?
			// rule X() = 'x' ==> rule X() { 'x'; }  ==> #def(X, #(), rule, #{}('x'))
			// .              ==> _                  ==> _
			// 'a' B C        ==> ('A', B, C)        ==> #tuple('A', B, C)
			// 'x' | X Y | Z  ==> 'X' | (X, Y) | Z   ==> #|(#|('X', (X, Y)), Z)
			// 'x' / X Y / Z  ==> 'X' / (X, Y) / Z   ==> #/(#/('X', (X, Y)), Z)
			// ~WS_CHAR       ==> ~WS_CHAR           ==> #~(WS_CHAR)
			// 'a'..'z'       ==> 'a'..'z'           ==> #..('a', 'z')
			// ('+'|'-')?     ==> ('+'|'-')`?`       ==> #`?`(#|('+','-'))
			// WS_CHAR+       ==> WS_CHAR`+`         ==> #`+`(WS_CHAR)
			// WS_CHAR*       ==> WS_CHAR`*`         ==> #`*`(WS_CHAR)
			// nongreedy(.)+  ==> _ `+min`           ==> #`+min`(WS_CHAR)
			// greedy(' ')*   ==> ' '`*max`          ==> #`*max`(WS_CHAR)
			// &('\\' '\\')   ==> &('\\' '\\')       ==> #&('\\' '\\')
			// &!('\\' '\\')  ==> &!('\\' '\\')      ==> #&(#!('\\' '\\')) => postprocessed to #`&!`('\\' '\\')
			// A {code;} B    ==> (A, {code;}, B)    ==> #tuple(A, #{}(code), B)
			// A &{code;} B   ==> (A, &{code;}, B)   ==> #tuple(A, #&(#{}(code)), B) => postprocessed to #`&{}`(code)
			// A(arg)         ==> A(arg)             ==> A(arg)
			// 
		}

	}

	public class PGFactory : CodeFactory
	{
		public static readonly Symbol _Star = GSymbol.Get("#`*`");
		public static readonly Symbol _Plus = GSymbol.Get("#`+`");
		public static readonly Symbol _Opt = GSymbol.Get("#`?`");
		public static readonly Symbol _AndNot = GSymbol.Get("#`&!`");
		public static readonly Symbol _AndCode = GSymbol.Get("#`&{}`");
		public static readonly Symbol _Nongreedy = GSymbol.Get("nongreedy");
		public static readonly Symbol _Greedy = GSymbol.Get("greedy");

		public static readonly Node _ = Symbol(GSymbol.Get("_"));
		public static Node Rule(string name, params Node[] sequence)
		{
			return Def(GSymbol.Get(name), ArgList(), Symbol("rule"), Braces(sequence));
		}
		public static Node S(params Node[] sequence) { return Call(_Tuple, sequence); }
		public static Node Star(params Node[] sequence) { return Call(_Star, AutoS(sequence)); }
		public static Node Plus(params Node[] sequence) { return Call(_Plus, AutoS(sequence)); }
		public static Node Opt(params Node[] sequence)  { return Call(_Opt,  AutoS(sequence)); }
		public static Node Nongreedy(Node loop) { return Greedy(loop, false); }
		public static Node Greedy(Node loop, bool greedy = true)
		{
			Debug.Assert(loop.Name == _Star || loop.Name == _Plus || loop.Name == _Opt);
			return Call(greedy ? _Greedy : _Nongreedy, loop);
		}
		public static Node And(params Node[] sequence)  { return Call(_AndBits, AutoS(sequence)); }
		public static Node AndNot(params Node[] sequence) { return Call(_AndNot, AutoS(sequence)); }
		public static Node AndCode(params Node[] sequence) { return Call(_AndCode, sequence); }
		public static Node Code(params Node[] statements) { return Call(_Braces, statements); }
		private static Node AutoS(Node[] sequence)
		{
			return sequence.Length == 1 ? sequence[0] : S(sequence);
		}
	}

	public struct @void
	{
		public static readonly @void Value = new @void();
	}


}