// Luban binary archive reader for Unreal Engine
// Reads Luban's varint-encoded binary format via Ar << value syntax.
//
// Usage:
//   FLubanArchive Ar;
//   if (Ar.LoadFromFile(TEXT("path/to/data.bin")))
//   {
//       FTables Tables;
//       Tables.Load(Ar);
//   }

#pragma once

#include "CoreMinimal.h"
#include "Misc/FileHelper.h"

class FLubanArchive
{
public:
    FLubanArchive()
        : ReadPos(0)
    {
    }

    // ---- Data loading ----

    /** Load binary data from a file path */
    bool LoadFromFile(const FString& FilePath)
    {
        TArray<uint8> FileData;
        if (!FFileHelper::LoadFileToArray(FileData, *FilePath))
        {
            return false;
        }
        Buffer = MoveTemp(FileData);
        ReadPos = 0;
        return true;
    }

    /** Load from already-owned byte array */
    void SetData(TArray<uint8>&& InData)
    {
        Buffer = MoveTemp(InData);
        ReadPos = 0;
    }

    /** Load from raw pointer + size */
    void SetData(const uint8* InData, int32 Size)
    {
        Buffer.SetNumUninitialized(Size);
        FMemory::Memcpy(Buffer.GetData(), InData, Size);
        ReadPos = 0;
    }

    /** Reset read cursor to beginning */
    void Reset()
    {
        ReadPos = 0;
    }

    /** Remaining unread bytes */
    int32 Remaining() const
    {
        return Buffer.Num() - ReadPos;
    }

    // ---- operator<< for all Luban types ----

    FLubanArchive& operator<<(bool& Value)
    {
        uint8 B = 0;
        ReadRaw(B);
        Value = (B != 0);
        return *this;
    }

    FLubanArchive& operator<<(uint8& Value)
    {
        ReadRaw(Value);
        return *this;
    }

    FLubanArchive& operator<<(int32& Value)
    {
        uint32 U;
        if (ReadVarUint32(U))
        {
            Value = static_cast<int32>(U);
        }
        else
        {
            Value = 0;
        }
        return *this;
    }

    FLubanArchive& operator<<(uint32& Value)
    {
        ReadVarUint32(Value);
        return *this;
    }

    FLubanArchive& operator<<(int64& Value)
    {
        uint64 U;
        if (ReadVarUint64(U))
        {
            Value = static_cast<int64>(U);
        }
        else
        {
            Value = 0;
        }
        return *this;
    }

    FLubanArchive& operator<<(uint64& Value)
    {
        ReadVarUint64(Value);
        return *this;
    }

    FLubanArchive& operator<<(float& Value)
    {
        ReadRaw(Value);
        return *this;
    }

    FLubanArchive& operator<<(double& Value)
    {
        ReadRaw(Value);
        return *this;
    }

    FLubanArchive& operator<<(FString& Value)
    {
        int32 Size = ReadSize();
        if (Size > 0 && HasData(Size))
        {
            // Luban stores strings as UTF-8 — use FUTF8ToTCHAR for proper conversion
            const ANSICHAR* Src = reinterpret_cast<const ANSICHAR*>(Buffer.GetData() + ReadPos);
            FUTF8ToTCHAR Converter(Src, Size);
            Value = FString(Converter.Length(), Converter.Get());
            ReadPos += Size;
        }
        else
        {
            Value.Empty();
        }
        return *this;
    }

    FLubanArchive& operator<<(FName& Value)
    {
        FString Str;
        *this << Str;
        Value = FName(*Str);
        return *this;
    }

    /** Read a size prefix (varint-encoded, used for containers and strings) */
    int32 ReadSize()
    {
        uint32 U = 0;
        ReadVarUint32(U);
        return static_cast<int32>(U);
    }

private:
    TArray<uint8> Buffer;
    int32 ReadPos;

    bool HasData(int32 Count) const
    {
        return ReadPos + Count <= Buffer.Num();
    }

    template<typename T>
    void ReadRaw(T& Value)
    {
        if (HasData(sizeof(T)))
        {
            FMemory::Memcpy(&Value, Buffer.GetData() + ReadPos, sizeof(T));
            ReadPos += sizeof(T);
        }
    }

    // ---- Varint decoding (same format as Luban's ByteBuf) ----

    /** Read a variable-length encoded uint32 (1-5 bytes) */
    bool ReadVarUint32(uint32& Out)
    {
        if (!HasData(1))
        {
            Out = 0;
            return false;
        }

        const uint32 H = Buffer[ReadPos];
        if (H < 0x80)
        {
            ReadPos++;
            Out = H;
        }
        else if (H < 0xC0)
        {
            if (!HasData(2)) { Out = 0; return false; }
            Out = ((H & 0x3F) << 8) | Buffer[ReadPos + 1];
            ReadPos += 2;
        }
        else if (H < 0xE0)
        {
            if (!HasData(3)) { Out = 0; return false; }
            Out = ((H & 0x1F) << 16) | (static_cast<uint32>(Buffer[ReadPos + 1]) << 8) | Buffer[ReadPos + 2];
            ReadPos += 3;
        }
        else if (H < 0xF0)
        {
            if (!HasData(4)) { Out = 0; return false; }
            Out = ((H & 0x0F) << 24) | (static_cast<uint32>(Buffer[ReadPos + 1]) << 16)
                | (static_cast<uint32>(Buffer[ReadPos + 2]) << 8) | Buffer[ReadPos + 3];
            ReadPos += 4;
        }
        else
        {
            if (!HasData(5)) { Out = 0; return false; }
            Out = (static_cast<uint32>(Buffer[ReadPos + 1]) << 24) | (static_cast<uint32>(Buffer[ReadPos + 2]) << 16)
                | (static_cast<uint32>(Buffer[ReadPos + 3]) << 8) | Buffer[ReadPos + 4];
            ReadPos += 5;
        }
        return true;
    }

    /** Read a variable-length encoded uint64 (1-9 bytes) */
    bool ReadVarUint64(uint64& Out)
    {
        if (!HasData(1))
        {
            Out = 0;
            return false;
        }

        const uint32 H = Buffer[ReadPos];
        if (H < 0x80)
        {
            ReadPos++;
            Out = H;
        }
        else if (H < 0xC0)
        {
            if (!HasData(2)) { Out = 0; return false; }
            Out = ((H & 0x3F) << 8) | Buffer[ReadPos + 1];
            ReadPos += 2;
        }
        else if (H < 0xE0)
        {
            if (!HasData(3)) { Out = 0; return false; }
            Out = ((H & 0x1F) << 16) | (static_cast<uint64>(Buffer[ReadPos + 1]) << 8) | Buffer[ReadPos + 2];
            ReadPos += 3;
        }
        else if (H < 0xF0)
        {
            if (!HasData(4)) { Out = 0; return false; }
            Out = ((H & 0x0F) << 24) | (static_cast<uint64>(Buffer[ReadPos + 1]) << 16)
                | (static_cast<uint64>(Buffer[ReadPos + 2]) << 8) | Buffer[ReadPos + 3];
            ReadPos += 4;
        }
        else if (H < 0xF8)
        {
            if (!HasData(5)) { Out = 0; return false; }
            uint64 Xl = (static_cast<uint64>(Buffer[ReadPos + 1]) << 24) | (static_cast<uint64>(Buffer[ReadPos + 2]) << 16)
                | (static_cast<uint64>(Buffer[ReadPos + 3]) << 8) | Buffer[ReadPos + 4];
            uint64 Xh = H & 0x07;
            ReadPos += 5;
            Out = (Xh << 32) | Xl;
        }
        else if (H < 0xFC)
        {
            if (!HasData(6)) { Out = 0; return false; }
            uint64 Xl = (static_cast<uint64>(Buffer[ReadPos + 2]) << 24) | (static_cast<uint64>(Buffer[ReadPos + 3]) << 16)
                | (static_cast<uint64>(Buffer[ReadPos + 4]) << 8) | Buffer[ReadPos + 5];
            uint64 Xh = ((H & 0x03) << 8) | Buffer[ReadPos + 1];
            ReadPos += 6;
            Out = (Xh << 32) | Xl;
        }
        else if (H < 0xFE)
        {
            if (!HasData(7)) { Out = 0; return false; }
            uint64 Xl = (static_cast<uint64>(Buffer[ReadPos + 3]) << 24) | (static_cast<uint64>(Buffer[ReadPos + 4]) << 16)
                | (static_cast<uint64>(Buffer[ReadPos + 5]) << 8) | Buffer[ReadPos + 6];
            uint64 Xh = ((H & 0x01) << 16) | (static_cast<uint64>(Buffer[ReadPos + 1]) << 8) | Buffer[ReadPos + 2];
            ReadPos += 7;
            Out = (Xh << 32) | Xl;
        }
        else if (H < 0xFF)
        {
            if (!HasData(8)) { Out = 0; return false; }
            uint64 Xl = (static_cast<uint64>(Buffer[ReadPos + 4]) << 24) | (static_cast<uint64>(Buffer[ReadPos + 5]) << 16)
                | (static_cast<uint64>(Buffer[ReadPos + 6]) << 8) | Buffer[ReadPos + 7];
            uint64 Xh = (static_cast<uint64>(Buffer[ReadPos + 1]) << 16) | (static_cast<uint64>(Buffer[ReadPos + 2]) << 8)
                | Buffer[ReadPos + 3];
            ReadPos += 8;
            Out = (Xh << 32) | Xl;
        }
        else
        {
            if (!HasData(9)) { Out = 0; return false; }
            uint64 Xl = (static_cast<uint64>(Buffer[ReadPos + 5]) << 24) | (static_cast<uint64>(Buffer[ReadPos + 6]) << 16)
                | (static_cast<uint64>(Buffer[ReadPos + 7]) << 8) | Buffer[ReadPos + 8];
            uint64 Xh = (static_cast<uint64>(Buffer[ReadPos + 1]) << 24) | (static_cast<uint64>(Buffer[ReadPos + 2]) << 16)
                | (static_cast<uint64>(Buffer[ReadPos + 3]) << 8) | Buffer[ReadPos + 4];
            ReadPos += 9;
            Out = (Xh << 32) | Xl;
        }
        return true;
    }
};
