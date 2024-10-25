import { HardhatUserConfig } from "hardhat/config";
import "@nomicfoundation/hardhat-toolbox";

// Get the PORT from the environment variables or default to 8545
const PORT = process.env.PORT || 8545;

// Set the chain ID based on the port
let chainId;
if (PORT == 8545) {
  chainId = 1337;
} else if (PORT == 8547) {
  chainId = 42161 //412346;
} else {
  throw new Error(`Unsupported port: ${PORT}`);
}

const config: HardhatUserConfig = {
  solidity: "0.8.24",  // or your preferred Solidity version
  networks: {
    hardhat: {
      chainId: chainId,
    },
  },
};

export default config;
