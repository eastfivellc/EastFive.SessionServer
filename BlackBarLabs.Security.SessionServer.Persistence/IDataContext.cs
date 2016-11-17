namespace BlackBarLabs.Security.SessionServer.Persistence
{
    public interface IDataContext
    {
        IAuthorizations Authorizations { get; }

        ISessions Sessions { get; }
    }
}
