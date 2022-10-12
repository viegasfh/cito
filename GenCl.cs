// GenCl.cs - OpenCL C code generator
//
// Copyright (C) 2020-2022  Piotr Fusik
//
// This file is part of CiTo, see https://github.com/pfusik/cito
//
// CiTo is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// CiTo is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with CiTo.  If not, see http://www.gnu.org/licenses/

using System;
using System.Collections.Generic;
using System.Linq;

namespace Foxoft.Ci
{

public class GenCl : GenC
{
	bool StringLength;
	bool StringEquals;
	bool StringStartsWith;
	bool BytesEqualsString;

	protected override void IncludeStdBool()
	{
	}

	protected override void IncludeMath()
	{
	}

	protected override void Write(TypeCode typeCode)
	{
		switch (typeCode) {
		case TypeCode.SByte:
			Write("char");
			break;
		case TypeCode.Byte:
			Write("uchar");
			break;
		case TypeCode.Int16:
			Write("short");
			break;
		case TypeCode.UInt16:
			Write("ushort");
			break;
		case TypeCode.Int32:
			Write("int");
			break;
		case TypeCode.Int64:
			Write("long");
			break;
		default:
			throw new NotImplementedException(typeCode.ToString());
		}
	}

	protected override void WriteStringPtrType() => Write("constant char *");

	public override CiExpr VisitInterpolatedString(CiInterpolatedString expr, CiPriority parent)
	{
		throw new NotImplementedException("Interpolated strings not supported in OpenCL C");
	}

	protected override void WriteCamelCaseNotKeyword(string name)
	{
		switch (name) {
		case "Constant":
		case "Global":
		case "Kernel":
		case "Local":
		case "Private":
		case "constant":
		case "global":
		case "kernel":
		case "local":
		case "private":
			WriteCamelCase(name);
			Write('_');
			break;
		default:
			base.WriteCamelCaseNotKeyword(name);
			break;
		}
	}

	protected override string GetConst(CiArrayStorageType array) => array.PtrTaken ? "const " : "constant ";

	protected override void WriteSubstringEqual(bool cast, CiExpr ptr, CiExpr offset, string literal, CiPriority parent, bool not)
	{
		if (not)
			Write('!');
		if (cast) {
			this.BytesEqualsString = true;
			Write("CiBytes_Equals(");
		}
		else {
			this.StringStartsWith = true;
			Write("CiString_StartsWith(");
		}
		WriteArrayPtrAdd(ptr, offset);
		Write(", ");
		VisitLiteralString(literal);
		Write(')');
	}

	protected override void WriteEqualStringInternal(CiExpr left, CiExpr right, CiPriority parent, bool not)
	{
		this.StringEquals = true;
		if (not)
			Write('!');
		WriteCall("CiString_Equals", left, right);
	}

	protected override void WriteStringLength(CiExpr expr)
	{
		this.StringLength = true;
		WriteCall("strlen", expr);
	}

	void WriteConsoleWrite(List<CiExpr> args, bool newLine)
	{
		Write("printf(");
		if (args.Count == 0)
			Write("\"\\n\")");
		else if (args[0] is CiInterpolatedString interpolated)
			WritePrintf(interpolated, newLine);
		else {
			Write("\"%");
			Write(args[0].Type is CiIntegerType ? 'd' : args[0].Type is CiFloatingType ? 'g' : 's');
			if (newLine)
				Write("\\n");
			Write("\", ");
			args[0].Accept(this, CiPriority.Argument);
			Write(')');
		}
	}

	protected override void WriteCall(CiExpr obj, CiMethod method, List<CiExpr> args, CiPriority parent)
	{
		switch (method.Id) {
		case CiId.StringStartsWith:
			if (IsOneAsciiString(args[0], out char c)) {
				if (parent > CiPriority.Equality)
					Write('(');
				obj.Accept(this, CiPriority.Primary);
				Write("[0] == ");
				VisitLiteralChar(c);
				if (parent > CiPriority.Equality)
					Write(')');
			}
			else {
				this.StringStartsWith = true;
				WriteCall("CiString_StartsWith", obj, args[0]);
			}
			break;
		case CiId.StringSubstring:
			if (args.Count != 1)
				throw new NotImplementedException("Substring");
			if (parent > CiPriority.Add)
				Write('(');
			WriteAdd(obj, args[0]);
			if (parent > CiPriority.Add)
				Write(')');
			break;
		case CiId.ArrayCopyTo:
			Write("for (size_t _i = 0; _i < ");
			args[3].Accept(this, CiPriority.Rel); // FIXME: side effect in every iteration
			WriteLine("; _i++)");
			Write('\t');
			args[1].Accept(this, CiPriority.Primary); // FIXME: side effect in every iteration
			Write('[');
			StartAdd(args[2]); // FIXME: side effect in every iteration
			Write("_i] = ");
			obj.Accept(this, CiPriority.Primary); // FIXME: side effect in every iteration
			Write('[');
			StartAdd(args[0]); // FIXME: side effect in every iteration
			Write("_i]");
			break;
		case CiId.ArrayFillAll:
		case CiId.ArrayFillPart:
			WriteArrayFill(obj, args);
			break;
		case CiId.ConsoleWrite:
			WriteConsoleWrite(args, false);
			break;
		case CiId.ConsoleWriteLine:
			WriteConsoleWrite(args, true);
			break;
		case CiId.UTF8GetByteCount:
			WriteStringLength(args[0]);
			break;
		case CiId.UTF8GetBytes:
			Write("for (size_t _i = 0; ");
			args[0].Accept(this, CiPriority.Primary); // FIXME: side effect in every iteration
			WriteLine("[_i] != '\\0'; _i++)");
			Write('\t');
			args[1].Accept(this, CiPriority.Primary); // FIXME: side effect in every iteration
			Write('[');
			StartAdd(args[2]); // FIXME: side effect in every iteration
			Write("_i] = ");
			args[0].Accept(this, CiPriority.Primary); // FIXME: side effect in every iteration
			Write("[_i]");
			break;
		case CiId.MathMethod:
		case CiId.MathIsFinite:
		case CiId.MathIsNaN:
		case CiId.MathLog2:
			WriteLowercase(method.Name);
			WriteArgsInParentheses(method, args);
			break;
		case CiId.MathCeiling:
			WriteCall("ceil", args[0]);
			break;
		case CiId.MathFusedMultiplyAdd:
			WriteCall("fma", args[0], args[1], args[2]);
			break;
		case CiId.MathIsInfinity:
			WriteCall("isinf", args[0]);
			break;
		case CiId.MathTruncate:
			WriteCall("trunc", args[0]);
			break;
		default:
			WriteCCall(obj, method, args);
			break;
		}
	}

	public override void VisitAssert(CiAssert statement)
	{
	}

	protected override void WriteCaseBody(List<CiStatement> statements)
	{
		if (statements.All(statement => statement is CiAssert))
			WriteLine(';');
		else
			base.WriteCaseBody(statements);
	}

	void WriteLibrary()
	{
		if (this.StringLength) {
			WriteLine();
			WriteLine("static int strlen(constant char *str)");
			OpenBlock();
			WriteLine("int len = 0;");
			WriteLine("while (str[len] != '\\0')");
			WriteLine("\tlen++;");
			WriteLine("return len;");
			CloseBlock();
		}
		if (this.StringEquals) {
			WriteLine();
			WriteLine("static bool CiString_Equals(constant char *str1, constant char *str2)");
			OpenBlock();
			WriteLine("for (size_t i = 0; str1[i] == str2[i]; i++) {");
			WriteLine("\tif (str1[i] == '\\0')");
			WriteLine("\t\treturn true;");
			WriteLine('}');
			WriteLine("return false;");
			CloseBlock();
		}
		if (this.StringStartsWith) {
			WriteLine();
			WriteLine("static bool CiString_StartsWith(constant char *str1, constant char *str2)");
			OpenBlock();
			WriteLine("for (int i = 0; str2[i] != '\\0'; i++) {");
			WriteLine("\tif (str1[i] != str2[i])");
			WriteLine("\t\treturn false;");
			WriteLine('}');
			WriteLine("return true;");
			CloseBlock();
		}
		if (this.BytesEqualsString) {
			WriteLine();
			WriteLine("static bool CiBytes_Equals(const uchar *mem, constant char *str)");
			OpenBlock();
			WriteLine("for (int i = 0; str[i] != '\\0'; i++) {");
			WriteLine("\tif (mem[i] != str[i])");
			WriteLine("\t\treturn false;");
			WriteLine('}');
			WriteLine("return true;");
			CloseBlock();
		}
	}

	public override void Write(CiProgram program)
	{
		this.WrittenClasses.Clear();
		this.StringLength = false;
		this.StringEquals = false;
		this.StringStartsWith = false;
		this.BytesEqualsString = false;
		OpenStringWriter();
		foreach (CiClass klass in program.Classes) {
			this.CurrentClass = klass;
			WriteConstructor(klass);
			WriteDestructor(klass);
			WriteMethods(klass);
		}

		CreateFile(this.OutputFile);
		WriteTopLevelNatives(program);
		WriteTypedefs(program, true);
		foreach (CiClass klass in program.Classes)
			WriteSignatures(klass, true);
		WriteTypedefs(program, false);
		foreach (CiClass klass in program.Classes)
			WriteClass(klass, program);
		WriteResources(program.Resources);
		WriteLibrary();
		CloseStringWriter();
		CloseFile();
	}
}

}

