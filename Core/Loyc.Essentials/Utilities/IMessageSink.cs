﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Loyc.Threading;
using System.Diagnostics;
using Loyc.Collections.Impl;
using Loyc.Collections;
using Loyc.Math;

namespace Loyc
{
	/// <summary>A general-purpose interface for a class that accepts formatted 
	/// messages with context information.</summary>
	/// <remarks>
	/// IMessageSink is used for dependency injection of a target for formatted 
	/// messages; it could be used for log messages, compiler error messages, or
	/// to report the progress of a process, for instance.
	/// <para/>
	/// It is typical to use <see cref="IMessageSink"/> without type parameters.
	/// <para/>
	/// Since .NET does not allow static members in an interface, the static
	/// members can be found in <see cref="MessageSink"/>.
	/// <para/>
	/// Each message has a <see cref="Severity"/>. For message sinks that are used 
	/// as loggers, this should be one of the following logging levels, listed
	/// in order of importance: Fatal, Error, Warning, Note, Debug, Verbose.
	/// <para/>
	/// For message sinks that are used for compiler messages, the standard
	/// levels are: Fatal, Error, Warning, Note, Detail. "Detail" provides more 
	/// information about a previously-printed message, while "Note" is intended 
	/// for independent messages that are less severe than warnings (e.g. lints).
	/// Some compilers may distiguish "soft" errors (which do not prevent the
	/// program from starting) from "critical" errors (which do). In that case, 
	/// Error may represent such a "soft" error and Critical may represent a
	/// "hard" error. Fatal, in contrast, represents an error that causes the
	/// compiler to halt immediately.
	/// <para/>
	/// If the message sink may produce messages that users will see, the message 
	/// sink itself should perform localization using <see 
	/// cref="Localize.Localized(string, object[])"/>.
	/// <para/>
	/// Only a single Write() method is truly needed (<see cref="Write(Severity, object, string, object[])"/>),
	/// but for efficiency reasons the interface contains two other writers. It 
	/// is expected to be fairly common that a message sink will drop some or
	/// all messages without printing them, e.g. if a message sink is used for 
	/// logging, verbose messages might be "off" by default. It would be 
	/// wasteful to actually localize and format a message if the message will
	/// not actually be printed, and it would also be wasteful to create an array 
	/// of objects to hold the arguments if they are just going to be discarded.
	/// With that in mind, since most formatting requests only need a couple of 
	/// arguments, there is an overload of Write() that accepts up to two
	/// arguments without the need to package them into an array, and there is
	/// an overload that takes no formatting arguments (this indicates that 
	/// parameter substitution is not required and should not be attempted.)
	/// <para/>
	/// In addition, the caller can call <see cref="IsEnabled(Severity)"/> to avoid 
	/// doing any work required to prepare a message for printing when a certain
	/// category of output is disabled.
	/// </remarks>
	/// <typeparam name="TSeverity">The type of the first parameter to <c>Write</c>, 
	/// which is used to indicate the "kind" of message being logged. Typically this 
	/// parameter is <see cref="Severity"/>, which includes values like Note, Warning 
	/// and Error.</typeparam>
	/// <typeparam name="TContext">The type of the second parameer to <c>Write</c>, 
	/// which indicates where the error occurs. If the message relates to a text
	/// file or source code, the location is typically indicated with an object of
	/// type <see cref="Loyc.Syntax.SourceRange"/> or <see cref="Loyc.Syntax.LNode"/>.</typeparam>
	/// <seealso cref="MessageSink"/>
	/// <seealso cref="ConsoleMessageSink"/>
	/// <seealso cref="TraceMessageSink"/>
	/// <seealso cref="NullMessageSink"/>
	/// <seealso cref="MessageFilter"/>
	/// <seealso cref="MessageHolder"/>
	/// <seealso cref="MessageSplitter"/>
	/// <seealso cref="IHasLocation"/>
	public interface IMessageSink<in TContext>
	{
		/// <summary>Writes a message to the target that this object represents.</summary>
		/// <param name="type">Severity or importance of the message; widely-used
		/// types include Error, Warning, Note, Debug, and Verbose. The special 
		/// type Detail is intended to provide more information about a previous 
		/// message.</param>
		/// <param name="context">An object that the message is related to, or that 
		/// represents the location that the message applies to. The message sink
		/// may try to convert this object to a string and include it in its output.
		/// See also <see cref="MessageSink.LocationString"/>().</param>
		/// <param name="format">A message to display. If there are additional 
		/// arguments, placeholders such as {0} and {1} refer to these arguments.</param>
		void Write(Severity level, TContext context, [Localizable] string format);
		void Write(Severity level, TContext context, [Localizable] string format, object arg0, object arg1 = null);
		void Write(Severity level, TContext context, [Localizable] string format, params object[] args);
		
		/// <summary>Returns true if messages of the specified type will actually be 
		/// printed, or false if Write(type, ...) has no effect.</summary>
		bool IsEnabled(Severity level);
	}
	
	/// <summary>Alias for IMessageSink&lt;object>.</summary>
	/// <seealso cref="IMessageSink{TContext}"/>
	public interface IMessageSink : IMessageSink<object>
	{
	}

	/// <summary>A linear scale to categorize the importance and seriousness of 
	/// messages sent to <see cref="IMessageSink"/>.</summary>
	/// <remarks>
	/// The numbers are Fatal=110, Critical=90, Error=70, Warning=60, Note=50, 
	/// Debug=30, Verbose=10 and Detail=0. The severity numbers are based on 
	/// those defined in log4net, divided by 1000, e.g. Warning=60000 in log4net
	/// but 60 in this enum.
	/// <para/>
	/// Some of the enumeration values begin with an underscore. These are
	/// values defined by Log4net that are deprecated in LoycCore.
	/// <para/>
	/// The "Detail" severity levels are meant to contain extra information 
	/// associated with the most recent non-Detail message, e.g. stack traces 
	/// or extra diagnostic information for Errors.
	/// </remarks>
	public enum Severity
	{
		VerboseDetail = 9,
		Verbose = 10,     // log4net: Verbose = Finest = 10000
		_Finer = 20,      // log4net: Finer = 20000
		DebugDetail = 29,
		Debug = 30,       // log4net: Debug = Fine = 30000
		CommonDetail = 35,      
		Common = 36,      // Common event: No log4net equivalent
		InfoDetail = 39,
		Info = 40,        // log4net: Info = 40000
		NoteDetail = 49,
		Note = 50,        // log4net: Notice = 50000
		WarningDetail = 59,
		Warning = 60,     // log4net: Warning = 60000
		UncommonDetail = 65,
		Uncommon = 66,    // Uncommon event: No log4net equivalent
		ErrorDetail = 69,
		Error = 70,       // log4net: Error = 70000
		RareDetail = 79,
		Rare = 80,        // log4net: Severe = 80000
		CriticalDetail = 89,
		Critical = 90,    // log4net: Critical = 90000
		_Alert = 100,     // log4net: Alert = 100000
		FatalDetail = 109,
		Fatal = 110,      // log4net: Fatal = 110000
		_Emergency = 120, // log4net: Emergency = 120000
	}

	/// <summary>This interface allows an object to declare its "location".</summary>
	/// <remarks>For example, <see cref="Loyc.Syntax.LNode"/> implements this 
	/// interface so that when a compiler error refers to a source code construct,
	/// the error message contains the location of that source code rather than the
	/// code itself.
	/// <para/>
	/// Given a context object that may or may not implement this interface, it's
	/// handy to use <see cref="MessageSink.LocationString"/> to convert the 
	/// "context" of a message into a string, or <see cref="MessageSink.LocationOf(object)"/> 
	/// to unwrap objects that implement IHasLocation.
	/// </remarks>
	public interface IHasLocation
	{
		object Location { get; }
	}

	/// <summary>This is the method signature of <c>IMessageSink.Write()</c>. You 
	/// can convert from one of these delegates to <see cref="IMessageSink"/> by 
	/// calling <see cref="MessageSink.FromDelegate"/>.</summary>
	/// <param name="type">Severity or importance of the message; widely-used
	/// types include Error, Warning, Note, Debug, and Verbose. The special 
	/// type Detail is intended to provide more information about a previous 
	/// message.</param>
	/// <param name="context">An object that represents the location that the
	/// message applies to, a string that indicates what the program was doing 
	/// when the message was generated, or any other relevant context information.
	/// See also <see cref="MessageSink.LocationString"/>().</param>
	/// <param name="format">A message to display. If there are additional 
	/// arguments, placeholders such as {0} and {1} refer to these arguments.</param>
	/// <param name="args">Optional arguments to fill in placeholders in the format 
	/// string.</param>
	public delegate void WriteMessageFn(Severity type, object context, string format, params object[] args);

	/// <summary>An exception that includes a "context" object as part of a
	/// <see cref="LogMessage"/> structure, typically used to indicate where an 
	/// error occurred.</summary>
	public class LogException : Exception
	{
		public LogException(object context, string format, params object[] args) : this(Severity.Error, context, format, args) {}
		public LogException(Severity severity, object context, string format, params object[] args) : this(new LogMessage(severity, context, format, args)) {}
		public LogException(LogMessage msg) { 
			Msg = msg;
			try {
				Data["Severity"] = msg.Severity;
				// Disabled because members of the Data dictionary must be serializable,
				// but msg.Context might not be. We could convert to string, but is it
				// worth the performance cost? Loyc code isn't really using Data anyway.
				//Data["Context"] = msg.Context;
			} catch { }
		}
		
		/// <summary>Contains additional information about the error that occurred.</summary>
		public LogMessage Msg { get; private set; }

		public override string Message
		{
			get { return Msg.Formatted; }
		}
	}
}
