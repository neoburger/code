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
    public class NeoBurgerGovernanceToken : Nep17Token
    {
        private const byte PREFIX_TEE = 0x50;
        public override byte Decimals() => 8;
        public override string Symbol() => "NOBUG";

        public static UInt160 GetTEE() => (UInt160)Storage.Get(Storage.CurrentContext, new byte[] { PREFIX_TEE });

        public static object ExecuteProposal(UInt160 scripthash, string method, ByteString[] args)
        {
            Runtime.CheckWitness((UInt160)Storage.Get(Storage.CurrentContext, new byte[] { PREFIX_TEE }));
            return Contract.Call(scripthash, method, CallFlags.All, args);
        }

        public static void ChangeTEE(UInt160 newTEE)
        {
            ByteString new_tee_bytearray = (ByteString)new byte[] { PREFIX_TEE };
            Runtime.CheckWitness((UInt160)Storage.Get(Storage.CurrentContext, new_tee_bytearray));
            Storage.Put(Storage.CurrentContext, new_tee_bytearray, newTEE);
        }

        public static void OnNEP17Payment(UInt160 from, BigInteger amount, object data)
        {
        }

        public static void Update(ByteString nefFile, string manifest)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Runtime.ExecutingScriptHash));
            ContractManagement.Update(nefFile, manifest, null);
        }
    }
}
