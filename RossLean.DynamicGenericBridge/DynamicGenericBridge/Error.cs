using Microsoft.CodeAnalysis;
using System;
using System.Linq;

namespace RossLean.DynamicGenericBridge
{
    internal class Error : Exception
    {
        public int Id { get; }
        public new FormattableString Message { get; }
        public Location? Location { get; }

        public Error(int id, FormattableString message, ISymbol? symbol) : base(message.ToString())
        {
            Id = id;
            Message = message;
            Location = symbol?.Locations.FirstOrDefault();
        }
    }
}
