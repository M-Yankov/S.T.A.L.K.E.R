namespace Santase.AI.StalkerPlayer.CardHelpers
{
    using Santase.AI.StalkerPlayer.Common;
    using Santase.AI.StalkerPlayer.Contracts;
    using Santase.Logic.Cards;

    public class CardHelper : ICardHelper
    {
        public int GetCardPriority(Card card)
        {
            switch (card.Type)
            {
                case CardType.Nine:
                    return 0;
                case CardType.Jack:
                    return 0;
                case CardType.Queen:
                    return 1;
                case CardType.King:
                    return 1;
                case CardType.Ace:
                    return 2;
                case CardType.Ten:
                    return 2;
                default:
                    return 0;
            }
        }
    }
}
