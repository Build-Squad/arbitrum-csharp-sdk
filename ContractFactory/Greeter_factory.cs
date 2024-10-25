using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;

namespace Arbitrum.ContractFactory
{
    public partial class GreeterDeployment : GreeterDeploymentBase
    {
        public GreeterDeployment() : base(BYTECODE) { }
        public GreeterDeployment(string byteCode) : base(byteCode) { }
    }

    public class GreeterDeploymentBase : ContractDeploymentMessage
    {
        public static string BYTECODE;
        public GreeterDeploymentBase() : base(BYTECODE) { }
        public GreeterDeploymentBase(string byteCode) : base(byteCode) 
        {
            BYTECODE = byteCode;
        }

    }

    public partial class DeployerFunction : DeployerFunctionBase { }

    [Function("deployer", "address")]
    public class DeployerFunctionBase : FunctionMessage
    {

    }

    public partial class GreetFunction : GreetFunctionBase { }

    [Function("greet", "string")]
    public class GreetFunctionBase : FunctionMessage
    {

    }

    public partial class SetGreetingFunction : SetGreetingFunctionBase { }

    [Function("setGreeting")]
    public class SetGreetingFunctionBase : FunctionMessage
    {
        [Parameter("string", "_greeting", 1)]
        public virtual string Greeting { get; set; }
    }

    public partial class DeployerOutputDTO : DeployerOutputDTOBase { }

    [FunctionOutput]
    public class DeployerOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("address", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }

    public partial class GreetOutputDTO : GreetOutputDTOBase { }

    [FunctionOutput]
    public class GreetOutputDTOBase : IFunctionOutputDTO
    {
        [Parameter("string", "", 1)]
        public virtual string ReturnValue1 { get; set; }
    }
}
