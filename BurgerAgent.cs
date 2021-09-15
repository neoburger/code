using System;
using System.ComponentModel;
using System.Numerics;
using Neo;
using Neo.SmartContract;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Services;


namespace NeoBurger
{
    [ManifestExtra("Author", "NEOBURGER")]
    [ManifestExtra("Email", "developer@neo.org")]
    [ManifestExtra("Description", "Agent Contract")]
    [ContractPermission("*", "*")]
    public class BurgerAgent : SmartContract
    {
        [InitialValue("[TODO]: ARGS", ContractParameterType.Hash160)]
        private static readonly UInt160 CORE = default;

        public static void Transfer(UInt160 to, BigInteger amount)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(CORE));
            ExecutionEngine.Assert(NEO.Transfer(Runtime.ExecutingScriptHash, to, amount));
        }

        public static void Sync()
        {
            ExecutionEngine.Assert(NEO.Transfer(Runtime.ExecutingScriptHash, Runtime.ExecutingScriptHash, 0));
        }

        public static void Claim()
        {
            ExecutionEngine.Assert(GAS.Transfer(Runtime.ExecutingScriptHash, CORE, GAS.BalanceOf(Runtime.ExecutingScriptHash), true));
        }

        public static void Vote(Neo.Cryptography.ECC.ECPoint target)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(CORE));
            ExecutionEngine.Assert(NEO.Vote(Runtime.ExecutingScriptHash, target));
        }

        public static void OnNEP17Payment(UInt160 from, BigInteger amount, object data)
        {
        }

        public static void Update(ByteString nefFile, string manifest)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness((UInt160)Contract.Call(CORE, "owner", CallFlags.All)));
            ContractManagement.Update(nefFile, manifest, null);
        }
    }
}
