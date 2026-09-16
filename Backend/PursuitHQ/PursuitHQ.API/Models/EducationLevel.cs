namespace PursuitHQ.API.Models
{
    /// <summary>
    /// What kind of institution a student is at.
    ///
    /// Stored as the underlying int, so these numbers are part of the data:
    /// append, never reorder or renumber. NotSet is 0 because that is what every
    /// existing row gets when the column is added, and "has not said" is a real
    /// answer that should not be confused with any of the others.
    /// </summary>
    public enum EducationLevel
    {
        NotSet = 0,
        HighSchool = 1,
        College = 2,
        University = 3,
        Other = 4
    }
}
