namespace Quizzer.DataModels.Enumerations
{
    /// <summary>
    /// Definiert die möglichen Datenbankoperationen eines Controllers als Flags-Enum.
    /// Mehrere Werte können per bitweisem OR kombiniert werden, z.B. in den Hooks
    /// <c>BeforeActionAsync</c> und <c>AfterActionAsync</c> des <c>GenericController</c>.
    /// </summary>
    [Flags]
    public enum Actions
    {
        /// <summary>Keine Aktion.</summary>
        None = 0,

        /// <summary>Neuen Datensatz einfügen (INSERT).</summary>
        Insert = 1,

        /// <summary>Bestehenden Datensatz aktualisieren (UPDATE).</summary>
        Update = Insert * 2,

        /// <summary>Datensatz löschen (DELETE).</summary>
        Delete = Update * 2,

        /// <summary>Einzelnen Datensatz anhand der ID laden (SELECT by ID).</summary>
        Get = Delete * 2,

        /// <summary>Alle Datensätze einer Entität laden (SELECT all).</summary>
        GetAll = Get * 2,

        /// <summary>Ausstehende EF-Core-Änderungen in die Datenbank schreiben (SaveChanges).</summary>
        SaveChanges = GetAll * 2,

        /// <summary>Kombinationsgruppe: nur Insert-Operationen.</summary>
        InsertActions = Insert,

        /// <summary>Kombinationsgruppe: nur Update-Operationen.</summary>
        UpdateActions = Update,

        /// <summary>Kombinationsgruppe: nur Delete-Operationen.</summary>
        DeleteActions = Delete,

        /// <summary>Kombinationsgruppe: alle Lese-Operationen (Get und GetAll).</summary>
        GetActions = Get + GetAll,

        /// <summary>Kombinationsgruppe: Insert- und Update-Operationen zusammen (Upsert).</summary>
        InsertAndUpdateActions = InsertActions + UpdateActions,

        /// <summary>Kombinationsgruppe: alle schreibenden Operationen (Insert, Update, Delete).</summary>
        WriteActions = InsertActions + UpdateActions + DeleteActions,

        /// <summary>Kombinationsgruppe: alle lesenden Operationen.</summary>
        ReadActions = GetActions,

        /// <summary>Kombinationsgruppe: sämtliche Operationen (lesen und schreiben).</summary>
        All = WriteActions + ReadActions,
    }
}