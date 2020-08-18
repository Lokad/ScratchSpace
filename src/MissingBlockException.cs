using System;
using Lokad.ContentAddr;

namespace Lokad.ScratchSpace
{
    public sealed class MissingBlockException : Exception
    {
        public readonly uint Realm;
        public readonly Hash Hash;

        public MissingBlockException(uint realm, Hash hash)
            : base($"Missing scratch block {realm}/{hash}")
        {
            Realm = realm;
            Hash = hash;
        }
    }
}