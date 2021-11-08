// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    using System;

    public readonly struct FramePort : IEquatable<FramePort>
    {
        public const int Size = sizeof(byte);
        private readonly byte value;

        public FramePort(byte value) => this.value = value;

        public bool Equals(FramePort other) => this.value == other.value;
        public override bool Equals(object? obj) => obj is FramePort other && this.Equals(other);
        public override int GetHashCode() => this.value.GetHashCode();

        public static bool operator ==(FramePort left, FramePort right) => left.Equals(right);
        public static bool operator !=(FramePort left, FramePort right) => !left.Equals(right);

        public bool IsMacCommandFPort => this.value == 0;
        public bool IsMacLayerTestFPort => this.value == 224;

        public Span<byte> Write(Span<byte> buffer)
        {
            buffer[0] = this.value;
            return buffer[Size..];
        }
    }
}