﻿using Stratis.SmartContracts;

public class ByteArrayConversion : SmartContract
{
    public ByteArrayConversion(ISmartContractState smartContractState) : base(smartContractState)
    {
    }

    public static int BytesToInt(byte[] value)
    {
        return value[0] | (value[1] << 8) | (value[2] << 16) | (value[3] << 24);
    }

    public static uint BytesToUInt(byte[] value)
    {
        return (uint) value[0] | (uint) (value[1] << 8) | (uint) (value[2] << 16) | (uint) (value[3] << 24);
    }

    public static ulong BytesToULong(byte[] value)
    {
        return (ulong) value[0] 
            | (ulong)(value[1] << 8) 
            | (ulong)(value[2] << 16) 
            | (ulong)(value[3] << 24) 
            | (ulong)(value[4] << 32)
            | (ulong)(value[5] << 40)
            | (ulong)(value[6] << 48)
            | (ulong)(value[7] << 56);
    }

    public static long BytesToLong(byte[] value)
    {
        return (long)value[0]
            | (long)(value[1] << 8)
            | (long)(value[2] << 16)
            | (long)(value[3] << 24)
            | (long)(value[4] << 32)
            | (long)(value[5] << 40)
            | (long)(value[6] << 48)
            | (long)(value[7] << 56);
    }

    public static byte[] IntToBytes(int value)
    {
        unchecked
        {
            byte[] bytes = new byte[4];
            bytes[0] = (byte)value;
            bytes[1] = (byte)(value >> 8);
            bytes[2] = (byte)(value >> 16);
            bytes[3] = (byte)(value >> 24);
            return bytes;
        }
    }

    public static byte[] UIntToBytes(uint value)
    {
        unchecked
        {
            byte[] bytes = new byte[4];
            bytes[0] = (byte)value;
            bytes[1] = (byte)(value >> 8);
            bytes[2] = (byte)(value >> 16);
            bytes[3] = (byte)(value >> 24);
            return bytes;
        }
    }

    public static byte[] ULongToBytes(ulong value)
    {
        unchecked
        {
            byte[] bytes = new byte[8];
            bytes[0] = (byte)value;
            bytes[1] = (byte)(value >> 8);
            bytes[2] = (byte)(value >> 16);
            bytes[3] = (byte)(value >> 24);
            bytes[4] = (byte)(value >> 32);
            bytes[5] = (byte)(value >> 40);
            bytes[6] = (byte)(value >> 48);
            bytes[7] = (byte)(value >> 56);
            return bytes;
        }
    }

    public static byte[] LongToBytes(long value)
    {
        unchecked
        {
            byte[] bytes = new byte[8];
            bytes[0] = (byte)value;
            bytes[1] = (byte)(value >> 8);
            bytes[2] = (byte)(value >> 16);
            bytes[3] = (byte)(value >> 24);
            bytes[4] = (byte)(value >> 32);
            bytes[5] = (byte)(value >> 40);
            bytes[6] = (byte)(value >> 48);
            bytes[7] = (byte)(value >> 56);
            return bytes;
        }
    }
}
