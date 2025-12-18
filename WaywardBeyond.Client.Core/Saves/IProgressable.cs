namespace WaywardBeyond.Client.Core.Saves;

internal interface IProgressStage
{
    float GetProgress();
    
    string GetStatus();
}