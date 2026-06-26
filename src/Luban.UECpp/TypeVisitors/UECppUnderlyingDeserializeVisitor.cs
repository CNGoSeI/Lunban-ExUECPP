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

public class UECppUnderlyingDeserializeVisitor : ITypeFuncVisitor<string, string, int, ITypeFuncVisitor<string>, string>
{
    public static UECppUnderlyingDeserializeVisitor Ins { get; } = new();

    public string Accept(TBool type, string bufName, string fieldName, int depth, ITypeFuncVisitor<string> typeVisitor)
    {
        return $"Ar << {fieldName};";
    }

    public string Accept(TByte type, string bufName, string fieldName, int depth, ITypeFuncVisitor<string> typeVisitor)
    {
        return $"Ar << {fieldName};";
    }

    public string Accept(TShort type, string bufName, string fieldName, int depth, ITypeFuncVisitor<string> typeVisitor)
    {
        return $"Ar << {fieldName};";
    }

    public string Accept(TInt type, string bufName, string fieldName, int depth, ITypeFuncVisitor<string> typeVisitor)
    {
        return $"Ar << {fieldName};";
    }

    public string Accept(TLong type, string bufName, string fieldName, int depth, ITypeFuncVisitor<string> typeVisitor)
    {
        return $"Ar << {fieldName};";
    }

    public string Accept(TFloat type, string bufName, string fieldName, int depth, ITypeFuncVisitor<string> typeVisitor)
    {
        return $"Ar << {fieldName};";
    }

    public string Accept(TDouble type, string bufName, string fieldName, int depth, ITypeFuncVisitor<string> typeVisitor)
    {
        return $"Ar << {fieldName};";
    }

    public string Accept(TEnum type, string bufName, string fieldName, int depth, ITypeFuncVisitor<string> typeVisitor)
    {
        return $"Ar << {fieldName};";
    }

    public string Accept(TString type, string bufName, string fieldName, int depth, ITypeFuncVisitor<string> typeVisitor)
    {
        return $"Ar << {fieldName};";
    }

    public string Accept(TBean type, string bufName, string fieldName, int depth, ITypeFuncVisitor<string> typeVisitor)
    {
        return $"Ar << {fieldName};";
    }

    public string Accept(TDateTime type, string bufName, string fieldName, int depth, ITypeFuncVisitor<string> typeVisitor)
    {
        return $"Ar << {fieldName};";
    }

    public string Accept(TArray type, string bufName, string fieldName, int depth, ITypeFuncVisitor<string> typeVisitor)
    {
        var suffix = depth == 0 ? "" : $"_{depth}";
        return $"{{int32 n{suffix}; Ar << n{suffix}; {fieldName}.Reserve(n{suffix}); for(int32 i{suffix} = 0; i{suffix} < n{suffix}; ++i{suffix}) {{ {type.ElementType.Apply(typeVisitor)} _e{suffix}; {type.ElementType.Apply(this, bufName, $"_e{suffix}", depth + 1, typeVisitor)} {fieldName}.Add(MoveTemp(_e{suffix})); }}}}";
    }

    public string Accept(TList type, string bufName, string fieldName, int depth, ITypeFuncVisitor<string> typeVisitor)
    {
        var suffix = depth == 0 ? "" : $"_{depth}";
        return $"{{int32 n{suffix}; Ar << n{suffix}; {fieldName}.Reserve(n{suffix}); for(int32 i{suffix} = 0; i{suffix} < n{suffix}; ++i{suffix}) {{ {type.ElementType.Apply(typeVisitor)} _e{suffix}; {type.ElementType.Apply(this, bufName, $"_e{suffix}", depth + 1, typeVisitor)} {fieldName}.Add(MoveTemp(_e{suffix})); }}}}";
    }

    public string Accept(TSet type, string bufName, string fieldName, int depth, ITypeFuncVisitor<string> typeVisitor)
    {
        var suffix = depth == 0 ? "" : $"_{depth}";
        return $"{{int32 n{suffix}; Ar << n{suffix}; {fieldName}.Reserve(n{suffix}); for(int32 i{suffix} = 0; i{suffix} < n{suffix}; ++i{suffix}) {{ {type.ElementType.Apply(typeVisitor)} _e{suffix}; {type.ElementType.Apply(this, bufName, $"_e{suffix}", depth + 1, typeVisitor)} {fieldName}.Add(MoveTemp(_e{suffix})); }}}}";
    }

    public string Accept(TMap type, string bufName, string fieldName, int depth, ITypeFuncVisitor<string> typeVisitor)
    {
        var suffix = depth == 0 ? "" : $"_{depth}";
        return $"{{int32 n{suffix}; Ar << n{suffix}; {fieldName}.Reserve(n{suffix}); for(int32 i{suffix} = 0; i{suffix} < n{suffix}; ++i{suffix}) {{ {type.KeyType.Apply(typeVisitor)} _k{suffix}; {type.KeyType.Apply(this, bufName, $"_k{suffix}", depth + 1, typeVisitor)} {type.ValueType.Apply(typeVisitor)} _v{suffix}; {type.ValueType.Apply(this, bufName, $"_v{suffix}", depth + 1, typeVisitor)} {fieldName}.Add(MoveTemp(_k{suffix}), MoveTemp(_v{suffix})); }}}}";
    }
}
