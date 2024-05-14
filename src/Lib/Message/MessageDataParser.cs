using System;
using System.Numerics;
using Arbitrum.DataEntities;
using Nethereum.ABI;
using Nethereum.ABI.Decoders;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Util;
using System.Formats.Asn1;
using System.Text;
using Nethereum.Hex.HexTypes;
using System.Runtime.InteropServices;
using Nethereum.ABI.FunctionEncoding;

namespace Arbitrum.Message
{
    public static class SubmitRetryableMessageDataParser
    {
        public static RetryableMessageParams Parse(string eventData)
        {
            var functionCallDecoder = new FunctionCallDecoder();

            var result = functionCallDecoder.DecodeFunctionOutput<RetryableMessageParamsTest>(eventData);

            var a = 123;
            return new RetryableMessageParams();
        }
    }
}
