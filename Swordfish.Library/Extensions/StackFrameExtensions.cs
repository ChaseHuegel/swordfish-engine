using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Swordfish.Library.Extensions
{
    public static class StackFrameExtensions
    {
        public static string ToFormattedString(this IEnumerable<StackFrame> stackFrames)
        {
            StringBuilder builder = new StringBuilder();

            foreach (var frame in stackFrames)
                frame.AppendStringBuilder(builder);

            return builder.ToString();
        }

        public static string ToFormattedString(this StackFrame frame)
        {
            return frame.AppendStringBuilder().ToString();
        }

        private static StringBuilder AppendStringBuilder(this StackFrame frame, StringBuilder builder = null)
        {
            if (builder == null)
                builder = new StringBuilder();

            builder.Append(Environment.NewLine);
            builder.Append("\t");

            builder.Append("at ");
            builder.Append(frame.GetMethod().DeclaringType);
            builder.Append(".");
            builder.Append(frame.GetMethod().Name);
            builder.Append("(");
            for (int i = 0; i < frame.GetMethod().GetParameters().Count(); i++)
            {
                ParameterInfo parameter = frame.GetMethod().GetParameters()[i];
                builder.Append(parameter.GetType());
                builder.Append(" ");
                builder.Append(parameter.Name);

                if (i < frame.GetMethod().GetParameters().Count() - 1)
                    builder.Append(", ");
            }
            builder.Append(")");

            if (!string.IsNullOrWhiteSpace(frame.GetFileName()))
            {
                builder.Append(" in ");
                builder.Append(frame.GetFileName());
                builder.Append("(");
                builder.Append(frame.GetFileLineNumber());
                builder.Append(",");
                builder.Append(frame.GetFileColumnNumber());
                builder.Append(")");
            }

            return builder;
        }
    }
}
