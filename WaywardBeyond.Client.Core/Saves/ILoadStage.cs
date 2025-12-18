using System.Threading.Tasks;

namespace WaywardBeyond.Client.Core.Saves;

internal interface ILoadStage<in TContext> : IProgressStage
{
    Task Load(TContext context);
}