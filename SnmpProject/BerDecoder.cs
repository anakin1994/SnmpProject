﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace SnmpProject
{
    public static class BerDecoder
    {
        public static DecodedTypeNode Decode(byte[] bytes)
        {
            return DecodeObject(bytes, null);
        }

        public static string DecodeOid(byte[] bytes)
        {
            var sb = new StringBuilder();
            var start = 3;
            var end = start;
            while (end < bytes.Length)
            {
                while ((bytes[end] & 1 << 7) != 0)
                {
                    end++;
                }
                var current = bytes.Skip(start).Take(end - start + 1).ToArray();
                for (int i = 0; i < current.Length; i++)
                {
                    current[i] &= 127;
                }
                var value = 0;
                var exp = 0;
                for (int i = current.Length - 1; i >= 0; i--)
                {
                    for (int j = 0; j < 7; j++)
                    {
                        if ((current[i] & 1 << j) != 0)
                            value += (int)Math.Pow(2, exp);
                        exp++;
                    }
                }
                sb.Append(value);
                if (end < bytes.Length - 1)
                    sb.Append(".");
                start = end + 1;
                end = start;
            }
            return sb.ToString();
        }

        public static string DecodeValue(DecodedType type)
        {
            var sb = new StringBuilder();
            foreach (var b in type.Data)
            {
                sb.Append(Convert.ToChar(b));
            }
            return sb.ToString();
        }

        private static DecodedTypeNode DecodeObject(byte[] bytes, DecodedTypeNode parent)
        {
            var current = bytes;
            while (current.Any())
            {
                var res = new DecodedType();

                var identifier = current[0];
                if ((identifier & 1 << 7) == 0)
                {
                    res.Visibility = (identifier & 1 << 6) == 0 ? VisibilitClass.Universal : VisibilitClass.Application;
                }
                else
                {
                    res.Visibility = (identifier & 1 << 6) == 0
                        ? VisibilitClass.ContextSpecific
                        : VisibilitClass.Private;
                }

                res.IsConstructed = (identifier & 1 << 5) != 0;
                res.TypeTagId = identifier & 31;

                long length;
                int dataStart;
                if (current[1] <= 127)
                {
                    length = current[1];
                    dataStart = 2;
                }
                else
                {
                    length = BitConverter.ToInt64(current.Skip(2).Take(current[1] & 127).ToArray(), 0);
                    dataStart = current[1] & 127 + 2;
                }
                res.Length = length;
                res.Data = current.Skip(dataStart).Take((int)length).ToArray();

                var node = new DecodedTypeNode { Value = res, Children = new List<DecodedTypeNode>() };
                if (res.IsConstructed) //sequence
                {
                    DecodeObject(current.Skip(dataStart).Take((int)length).ToArray(), node);
                }
                if (parent != null)
                {
                    node.Parent = parent;
                    parent.Children.Add(node);
                }
                else
                {
                    return node;    //Only root returns a value
                }

                current = current.Skip(dataStart + (int)length).ToArray();
            }

            return null;
        }
    }
}
