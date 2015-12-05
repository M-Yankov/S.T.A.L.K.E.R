namespace Santase.AI.StalkerPlayer.Contracts
{
    using Santase.Logic.Cards;

    public interface ICardHelper
    {
        int GetCardPriority(Card card);
    }
}