namespace Quizzer.DataModels.Enumerations
{
    [Flags]
    public enum Actions
    {
        None = 0,

        Insert = 1,

        Update = Insert * 2,

        Delete = Update * 2,

        Get = Delete * 2,
        GetAll = Get * 2,

        SaveChanges = GetAll * 2,

        InsertActions = Insert,
        UpdateActions = Update,
        DeleteActions = Delete,
        GetActions = Get + GetAll,

        InsertAndUpdateActions = InsertActions + UpdateActions,

        WriteActions = InsertActions + UpdateActions + DeleteActions,
        ReadActions = GetActions,
        All = WriteActions + ReadActions,
    }
}