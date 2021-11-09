using System.Numerics;
using Neo;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Services;
using Neo.SmartContract.Framework.Attributes;
using Neo.SmartContract;

namespace NeoBurger
{
    [ManifestExtra("Author", "NEOBURGER")]
    [ManifestExtra("Email", "developer@neo.org")]
    [ManifestExtra("Description", "NeoBurger Governance Token")]
    [SupportedStandards("NEP-17")]
    [ContractPermission("*", "*")]
    public class NeoBurgerGovernanceTokenMainnet : Nep17Token
    {
        [InitialValue("[TODO]: ARGS", ContractParameterType.Hash160)]
        private const UInt160 OWNER = default;
        public override byte Decimals() => 8;
        public override string Symbol() => "NOBUG";
        public static object ExecuteProposal(UInt160 scripthash, string method, ByteString[] args)
        {
            Runtime.CheckWitness(OWNER);
            return Contract.Call(scripthash, method, CallFlags.All, args);
        }

        public static void OnNEP17Payment(UInt160 from, BigInteger amount, object data)
        {
        }

        public static void Update(ByteString nefFile, string manifest)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(OWNER));
            ContractManagement.Update(nefFile, manifest, null);
        }
    }
}
