// GenTyped.cs - C#/Java code generator
//
// Copyright (C) 2011-2016  Piotr Fusik
//
// This file is part of CiTo, see http://cito.sourceforge.net
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

public abstract class GenTyped : GenBase
{
	protected abstract TypeCode GetTypeCode(CiIntegerType integer, bool promote);

	protected abstract void Write(TypeCode typeCode);

	TypeCode GetTypeCode(CiType type, bool promote)
	{
		if (type is CiNumericType) {
			CiIntegerType integer = type as CiIntegerType;
			if (integer != null)
				return GetTypeCode(integer, promote);
			if (type == CiSystem.DoubleType)
				return TypeCode.Double;
			if (type == CiSystem.FloatType)
				return TypeCode.Single;
			throw new NotImplementedException(type.ToString());
		}
		else if (type == CiSystem.BoolType)
			return TypeCode.Boolean;
		else if (type == CiSystem.NullType)
			return TypeCode.Empty;
		else if (type is CiStringType)
			return TypeCode.String;
		return TypeCode.Object;
	}

	static bool IsNarrower(TypeCode left, TypeCode right)
	{
		switch (left) {
		case TypeCode.SByte:
			switch (right) {
			case TypeCode.Byte:
			case TypeCode.Int16:
			case TypeCode.UInt16:
			case TypeCode.Int32:
			case TypeCode.Int64:
				return true;
			default:
				return false;
			}
		case TypeCode.Byte:
			switch (right) {
			case TypeCode.SByte:
			case TypeCode.Int16:
			case TypeCode.UInt16:
			case TypeCode.Int32:
			case TypeCode.Int64:
				return true;
			default:
				return false;
			}
		case TypeCode.Int16:
			switch (right) {
			case TypeCode.UInt16:
			case TypeCode.Int32:
			case TypeCode.Int64:
				return true;
			default:
				return false;
			}
		case TypeCode.UInt16:
			switch (right) {
			case TypeCode.Int16:
			case TypeCode.Int32:
			case TypeCode.Int64:
				return true;
			default:
				return false;
			}
		case TypeCode.Int32:
			return right == TypeCode.Int64;
		default:
			return false;
		}
	}

	bool WriteCoerced(CiType type, CiExpr expr, TypeCode exprTypeCode)
	{
		TypeCode typeCode = GetTypeCode(type, false);
		if (IsNarrower(typeCode, exprTypeCode)) {
			Write('(');
			Write(typeCode);
			Write(") ");
			expr.Accept(this, CiPriority.Primary);
			return true;
		}
		return false;
	}

	protected override void WriteCoerced(CiType type, CiExpr expr)
	{
		if (!WriteCoerced(type, expr, GetTypeCode(expr.Type, expr.IntPromotion)))
			base.WriteCoerced(type, expr);
	}

	protected override void WriteCoercedLiteral(CiType type, CiExpr expr, CiPriority parent)
	{
		if (!(expr is CiLiteral) || !WriteCoerced(type, expr, TypeCode.Int32))
			base.WriteCoercedLiteral(type, expr, parent);
	}
}

}