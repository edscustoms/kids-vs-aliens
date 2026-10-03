/// <summary>An assigned component performs one synchronous action; false rejects the visit.</summary>
public interface IGameplayAction
{
    bool TryExecute(PlayerCharacter player);
}
