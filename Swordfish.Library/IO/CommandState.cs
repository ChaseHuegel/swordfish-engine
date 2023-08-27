namespace Swordfish.Library.IO
{
    public enum CommandState
    {
        //  TODO Is InProgress necessary? Commands were going to use a BehaviorTree but are now async.
        InProgress,
        Success,
        Failure,
    }
}
