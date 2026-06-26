// Copyright 2025 Code Philosophy
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using Luban.Types;
using Luban.TypeVisitors;

namespace Luban.UECpp.TypeVisitors;

public class UECppDeserializeVisitor : DecoratorFuncVisitor<string, string, int, string>
{
    public static UECppDeserializeVisitor Ins { get; } = new UECppDeserializeVisitor();

    public override string DoAccept(TType type, string bufName, string fieldName, int depth)
    {
        if (type.IsNullable)
        {
            string elementType = type.Apply(UECppUnderlyingDeclaringTypeNameVisitor.Ins);
            return $"{{ bool _has_value_; Ar << _has_value_; if(_has_value_) {{ {fieldName}.Emplace({(type.IsBean ? "" : $"{elementType}()")}); {type.Apply(UECppUnderlyingDeserializeVisitor.Ins, bufName, type.IsBean ? fieldName : $"{fieldName}.GetValue()", depth + 1, UECppDeclaringTypeNameVisitor.Ins)} }} else {{ {fieldName}.Reset(); }} }}";
        }
        else
        {
            return type.Apply(UECppUnderlyingDeserializeVisitor.Ins, bufName, fieldName, depth, UECppDeclaringTypeNameVisitor.Ins);
        }
    }
}
