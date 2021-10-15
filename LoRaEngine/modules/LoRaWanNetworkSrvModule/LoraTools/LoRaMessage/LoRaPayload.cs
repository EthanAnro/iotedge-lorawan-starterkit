// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.LoRaMessage
{
    using System;
    using System.Linq;
    using System.Security.Cryptography;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Utils;
    using Org.BouncyCastle.Crypto.Parameters;
    using Org.BouncyCastle.Security;

    /// <summary>
    /// The LoRaPayloadWrapper class wraps all the information any LoRa message share in common.
    /// </summary>
    public abstract class LoRaPayload
    {
        public LoRaMessageType LoRaMessageType { get; set; }

        /// <summary>
        /// Gets or sets raw byte of the message.
        /// </summary>
        public byte[] RawMessage { get; set; }

        /// <summary>
        /// Gets or sets mACHeader of the message.
        /// </summary>
        public Memory<byte> Mhdr { get; set; }

        /// <summary>
        /// Gets or sets message Integrity Code.
        /// </summary>
        public Memory<byte> Mic { get; set; }

        /// <summary>
        /// Gets or sets assigned Dev Address, TODO change??.
        /// </summary>
        public Memory<byte> DevAddr { get; set; }

        /// <summary>
        /// Gets the representation of the 32bit Frame counter to be used
        /// in the block if we are in 32bit mode.
        /// </summary>
        protected byte[] Server32BitFcnt { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="LoRaPayload"/> class.
        /// Wrapper of a LoRa message, consisting of the MIC and MHDR, common to all LoRa messages
        /// This is used for uplink / decoding.
        /// </summary>
        protected LoRaPayload(byte[] inputMessage)
        {
            RawMessage = inputMessage ?? throw new ArgumentNullException(nameof(inputMessage));
            Mhdr = new Memory<byte>(RawMessage, 0, 1);
            // MIC 4 last bytes
            Mic = new Memory<byte>(RawMessage, inputMessage.Length - 4, 4);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LoRaPayload"/> class.
        /// This is used for downlink, The field will be computed at message creation.
        /// </summary>
        protected LoRaPayload()
        {
        }

        /// <summary>
        /// Method to take the different fields and assemble them in the message bytes.
        /// </summary>
        /// <returns>the message bytes.</returns>
        public abstract byte[] GetByteMessage();

        /// <summary>
        /// Method to check a Mic.
        /// </summary>
        /// <param name="nwskey">The Network Secret Key.</param>
        /// <param name="server32BitFcnt">Explicit 32bit count to use for calculating the block.</param>
        public abstract bool CheckMic(string nwskey, uint? server32BitFcnt = null);

        /// <summary>
        /// Method to calculate the encrypted version of the payload.
        /// </summary>
        /// <param name="appSkey">the Application Secret Key.</param>
        /// <returns>the encrypted bytes.</returns>
        public abstract byte[] PerformEncryption(string appSkey);

        /// <summary>
        /// A Method to calculate the Mic of the message.
        /// </summary>
        /// <returns> the Mic bytes.</returns>
        public byte[] CalculateMic(string appKey, byte[] algoinput)
        {
            if (algoinput is null) throw new ArgumentNullException(nameof(algoinput));

            var mac = MacUtilities.GetMac("AESCMAC");
            var key = new KeyParameter(ConversionHelper.StringToByteArray(appKey));
            mac.Init(key);
            var rfu = new byte[1];
            rfu[0] = 0x0;
            mac.BlockUpdate(algoinput, 0, algoinput.Length);
            var result = MacUtilities.DoFinal(mac);
            Mic = result.Take(4).ToArray();
            return Mic.ToArray();
        }

        /// <summary>
        /// Calculate the Netwok and Application Server Key used to encrypt data and compute MIC.
        /// </summary>
        public static byte[] CalculateKey(LoRaPayloadKeyType keyType, byte[] appnonce, byte[] netid, byte[] devnonce, byte[] appKey)
        {
            if (keyType == LoRaPayloadKeyType.None) throw new InvalidOperationException("No key type selected.");

            var type = new byte[1];
            type[0] = (byte)keyType;
            using Aes aes = new AesManaged
            {
                Key = appKey,
#pragma warning disable CA5358 // Review cipher mode usage with cryptography experts
                // Cipher is part of the LoRaWAN specification
                Mode = CipherMode.ECB,
#pragma warning restore CA5358 // Review cipher mode usage with cryptography experts
                Padding = PaddingMode.None
            };

            var pt = type.Concat(appnonce).Concat(netid).Concat(devnonce).Concat(new byte[7]).ToArray();

            aes.IV = new byte[16];
            ICryptoTransform cipher;
#pragma warning disable CA5401 // Do not use CreateEncryptor with non-default IV
            // Part of the LoRaWAN specification
            cipher = aes.CreateEncryptor();
#pragma warning restore CA5401 // Do not use CreateEncryptor with non-default IV
            var key = cipher.TransformFinalBlock(pt, 0, pt.Length);
            return key;
        }

        public static bool TryCreateLoRaPayload(Rxpk rxpk, out LoRaPayload loRaPayloadMessage)
        {
            if (rxpk is null) throw new ArgumentNullException(nameof(rxpk));

            var convertedInputMessage = Convert.FromBase64String(rxpk.Data);
            var messageType = convertedInputMessage[0];

            switch (messageType)
            {
                case (int)LoRaMessageType.UnconfirmedDataUp:
                case (int)LoRaMessageType.ConfirmedDataUp:
                    loRaPayloadMessage = new LoRaPayloadData(convertedInputMessage);
                    break;

                case (int)LoRaMessageType.JoinRequest:
                    loRaPayloadMessage = new LoRaPayloadJoinRequest(convertedInputMessage);
                    break;

                default:
                    loRaPayloadMessage = null;
                    return false;
            }

            loRaPayloadMessage.LoRaMessageType = (LoRaMessageType)messageType;
            return true;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LoRaPayload"/> class.
        /// Constructor used by the simulator.
        /// </summary>
        public static bool TryCreateLoRaPayloadForSimulator(Txpk txpk, string appKey, out LoRaPayload loRaPayload)
        {
            if (txpk is null) throw new ArgumentNullException(nameof(txpk));

            if (txpk.Data != null)
            {
                var convertedInputMessage = Convert.FromBase64String(txpk.Data);
                switch ((LoRaMessageType)convertedInputMessage[0])
                {
                    case LoRaMessageType.JoinRequest:
                        loRaPayload = new LoRaPayloadJoinRequest();
                        return true;
                    case LoRaMessageType.JoinAccept:
                        loRaPayload = new LoRaPayloadJoinAccept(convertedInputMessage, appKey);
                        return true;
                    case LoRaMessageType.UnconfirmedDataDown:
                    case LoRaMessageType.UnconfirmedDataUp:
                    case LoRaMessageType.ConfirmedDataUp:
                    case LoRaMessageType.ConfirmedDataDown:
                        loRaPayload = new LoRaPayloadData();
                        return true;
                    default:
                        loRaPayload = null;
                        return false;
                }
            }

            loRaPayload = null;
            return false;
        }

        public void Reset32BitBlockInfo()
        {
            Server32BitFcnt = null;
        }

        public void Ensure32BitFcntValue(uint? server32bitFcnt)
        {
            if (Server32BitFcnt == null && server32bitFcnt.HasValue)
            {
                Server32BitFcnt = BitConverter.GetBytes(server32bitFcnt.Value);
            }
        }

        /// <summary>
        /// In 32bit mode, the server needs to infer the upper 16bits by observing
        /// the traffic between the device and the server. We keep a 32bit counter
        /// on the server and combine the upper 16bits with what the client sends us
        /// on the wire (lower 16bits). The result is the inferred counter as we
        /// assume it is on the client.
        /// </summary>
        /// <param name="payloadFcnt">16bits counter sent in the package.</param>
        /// <param name="fcnt">Current server frame counter holding 32bits.</param>
        /// <returns>The inferred 32bit framecounter value, with the higher 16bits holding the server
        /// observed counter information and the lower 16bits the information we got on the wire.</returns>
        public static uint InferUpper32BitsForClientFcnt(ushort payloadFcnt, uint fcnt)
        {
            const uint MaskHigher16 = 0xFFFF0000;

            // server represents the counter in 32bit so does the client, but only sends the lower 16bits
            // infering the upper 16bits from the current count
            var fcntServerUpper = fcnt & MaskHigher16;
            return fcntServerUpper | payloadFcnt;
        }
    }
}
