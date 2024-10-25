using System;

namespace Arbitrum.DataEntities
{
    /// <summary>
    /// Constants class containing Ethereum addresses and other constants.
    /// </summary>
    public static class Constants
    {
        // Ethereum addresses
        public const string NODE_INTERFACE_ADDRESS = "0x00000000000000000000000000000000000000C8";
        public const string ARB_SYS_ADDRESS = "0x0000000000000000000000000000000000000064";
        public const string ARB_RETRYABLE_TX_ADDRESS = "0x000000000000000000000000000000000000006E";
        public const string ARB_ADDRESS_TABLE_ADDRESS = "0x0000000000000000000000000000000000000066";
        public const string ARB_OWNER_PUBLIC = "000000000000000000000000000000000000006B";
        public const string ARB_GAS_INFO = "000000000000000000000000000000000000006C";
        public const string ARB_STATISTICS = "000000000000000000000000000000000000006F";

        // Other constants
        public const double ARB_MINIMUM_BLOCK_TIME_IN_SECONDS = 0.25;

        /**
         * The offset added to an L1 address to get the corresponding L2 address
         */
        public const string ADDRESS_ALIAS_OFFSET = "0x1111000000000000000000000000000000001111";

        /**
         * Address of the gateway a token will be assigned to if it is disabled
         */
        public const string DISABLED_GATEWAY = "0000000000000000000000000000000000000001";

        /**
         * If a custom token is enabled for arbitrum it will implement a function called
         * isArbitrumEnabled which returns this value. Integer: 0xa4b1
         */
        public const int CUSTOM_TOKEN_IS_ENABLED = 42161;

        public const int SEVEN_DAYS_IN_SECONDS = 7 * 24 * 60 * 60;
        public const string ADDRESS_ZERO = "0x0000000000000000000000000000000000000000";

    }

}
