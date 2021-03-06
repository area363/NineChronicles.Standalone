using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using GraphQL;
using GraphQL.Types;
using Libplanet;
using Libplanet.Action;
using Libplanet.Blockchain;
using Libplanet.Blocks;
using Libplanet.Store;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;

namespace NineChronicles.Headless.GraphTypes
{
    public class NodeStatusType : ObjectGraphType<NodeStatusType>
    {
        public bool BootstrapEnded { get; set; }

        public bool PreloadEnded { get; set; }
        
        public bool IsMining { get; set; }

        public BlockChain<NCAction> BlockChain { get; set; }

        public IStore Store { get; set; }

        public NodeStatusType()
        {
            Field<NonNullGraphType<BooleanGraphType>>(name: "bootstrapEnded",
                resolve: context => context.Source.BootstrapEnded);
            Field<NonNullGraphType<BooleanGraphType>>(name: "preloadEnded",
                resolve: context => context.Source.PreloadEnded);
            Field<NonNullGraphType<BlockHeaderType>>(name: "tip",
                resolve: context => BlockHeaderType.FromBlock(context.Source.BlockChain.Tip));
            Field<NonNullGraphType<ListGraphType<BlockHeaderType>>>(
                name: "topmostBlocks",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "limit",
                        Description = "The number of blocks to get."
                    },
                    new QueryArgument<AddressType>
                    {
                        Name = "miner",
                        Description = "List only blocks mined by the given address.  " +
                            "(List everything if omitted.)",
                        DefaultValue = null,
                    }
                ),
                description: "The topmost blocks from the current node.",
                resolve: context =>
                {
                    IEnumerable<Block<NCAction>> blocks =
                        GetTopmostBlocks(context.Source.BlockChain);
                    if (context.GetArgument<Address?>("miner") is { } miner)
                    {
                        blocks = blocks.Where(b => b.Miner.Equals(miner));
                    }

                    return blocks
                        .Take(context.GetArgument<int>("limit"))
                        .Select(BlockHeaderType.FromBlock);
                });
            Field<ListGraphType<TxIdType>>(
                name: "stagedTxIds",
                description: "Staged TxIds from the current node.",
                resolve: context => context.Source.BlockChain.GetStagedTransactionIds()
            );
            Field<NonNullGraphType<BlockHeaderType>>(name: "genesis",
                resolve: context => BlockHeaderType.FromBlock(context.Source.BlockChain.Genesis));
            Field<NonNullGraphType<BooleanGraphType>>(name: "isMining",
                description: "Whether it is mining.",
                resolve: context => context.Source.IsMining
            );
        }

        private IEnumerable<Block<T>> GetTopmostBlocks<T>(BlockChain<T> blockChain)
            where T : IAction, new()
        {
            Block<T> block = blockChain.Tip;

            while (true)
            {
                yield return block;
                if (block.PreviousHash is HashDigest<SHA256> prev)
                {
                    block = blockChain[prev];
                }
                else
                {
                    break;
                }
            }
        }
    }
}
