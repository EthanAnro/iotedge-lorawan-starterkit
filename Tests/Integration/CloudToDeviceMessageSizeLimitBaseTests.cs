// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Integration
{
    using LoRaTools.LoRaPhysical;
    using LoRaWan.Tests.Common;

    public class CloudToDeviceMessageSizeLimitBaseTests : MessageProcessorTestBase
    {
        public static Rxpk CreateUpstreamRxpk(bool isConfirmed, bool hasMacInUpstream, string datr, SimulatedDevice simulatedDevice)
        {
            Rxpk rxpk;
            string msgPayload;

            if (isConfirmed)
            {
                if (hasMacInUpstream)
                {
                    // Cofirmed message with Mac command in upstream
                    msgPayload = "02";
                    var confirmedMessagePayload = simulatedDevice.CreateConfirmedDataUpMessage(msgPayload, isHexPayload: true, fport: 0);
                    rxpk = confirmedMessagePayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey, datr: datr).Rxpk[0];
                }
                else
                {
                    // Cofirmed message without Mac command in upstream
                    msgPayload = "1234567890";
                    var confirmedMessagePayload = simulatedDevice.CreateConfirmedDataUpMessage(msgPayload);
                    rxpk = confirmedMessagePayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey, datr: datr).Rxpk[0];
                }
            }
            else
            {
                if (hasMacInUpstream)
                {
                    // Uncofirmed message with Mac command in upstream
                    msgPayload = "02";
                    var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage(msgPayload, isHexPayload: true, fport: 0);
                    rxpk = unconfirmedMessagePayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey, datr: datr).Rxpk[0];
                }
                else
                {
                    // Uncofirmed message without Mac command in upstream
                    msgPayload = "1234567890";
                    var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage(msgPayload);
                    rxpk = unconfirmedMessagePayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey, datr: datr).Rxpk[0];
                }
            }

            return rxpk;
        }
    }
}