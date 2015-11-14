##Team work documentation. drafts and other.♥♦♣♠
 1. Added logic for get other card from enemy.

 Observe final state. Or how to know in witch state is the game?
  Implement logic when enemy can't respond to the suit that out bot played.
 So maybe the enemy hasn't any from this suit;

we have this cards: J♥ 10♥ Q♣ 9♦ K♠ A♠ → logic to play: J♥ to extract
A♥ from enemy. But if not success _10♥_ remains alone!

    we have cards: 9♥ A♥ J♣ 10♦ J♠ K♠
    to not play J♠ (keep K♠ for Q♠ if not passed)

use separate collection for cards played form Stalker.
