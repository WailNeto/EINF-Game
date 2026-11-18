namespace DinoGenesis.Persistence
{
    public interface IGameSaveRepository
    {
        bool HasSave { get; }
        GameSaveData Load();
        void Save(GameSaveData saveData);
    }
}
