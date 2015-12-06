# S.T.A.L.K.E.R. Player AI v.0.1 
## for Santase Card Game

###  ♠♣♦♥ Documentation ♥♦♣♠

1. Initialization:
    * First of all, in the start of every round in the `StartRound()` method the AI player initializes a class `CardHolder.cs` responsible
	for storing information about all cards and their current status: (in S.T.A.L.K.E.R, EnemyOrDeck, Enemy or Passed).  [code](https://github.com/M-Yankov/S.T.A.L.K.E.R/blob/master/SantaseEngine/Source/AI/Santase.AI.StalkerPlayer/CardHelpers/CardHolder.cs#L42)
	* Every turn the status of the newly added card is updated to `in S.T.A.L.K.E.R.` via the `Add()` method from `BasePlayer`.
    * Via the `EndTurn()` method the status of the two played cards is updated to `Passed`. Both operations are performed with constant complexity.
   
1. Afterwards, the AI player tries to execute the following actions:
	*  In case the enemy has made an announce, the AI player changes the status of the other card from the announce to `inEnemy`. Example:
    ```
    Enemy announces 20 with `K♣`, the AI player changes the status of card `Q♣` to in enemy.
    ```
	* In the final state of the game, when the deck does not contain any more cards, the AI player refreshes the information about the enemy cards since they are known at that stage.
     `EnemyCards = AllCards - cards in S.T.A.L.K.E.R. and Passed cards.`
    [code](https://github.com/M-Yankov/S.T.A.L.K.E.R/blob/master/SantaseEngine/Source/AI/Santase.AI.StalkerPlayer/CardHelpers/CardHolder.cs#L60)
	* Exchange the trump card via the inherited `PlayerActionValidator` the by `BasePlayer` parent class.
	* Close the game if some specifically predefined requirements are met (for example AI player can announce 40 and has accumulated more than certain ammount of points).
	[code](https://github.com/M-Yankov/S.T.A.L.K.E.R/blob/master/SantaseEngine/Source/AI/Santase.AI.StalkerPlayer/CardHelpers/StalkerHelper.cs#L41)
1. Then, the AI player implements different logic depending on his play turn:
1. In case he has the **first turn**:
	* The AI player tries to make an announce following the Announce logic explained at the end of the documentation
	* Then the AI player plays differently according to the current game state:
     * In StartRoundState implements the **PriorityLogic**_ explained at the end of the documentation[code](https://github.com/M-Yankov/S.T.A.L.K.E.R/blob/master/SantaseEngine/Source/AI/Santase.AI.StalkerPlayer/CardHelpers/CardChooser.cs)
     * In MoreThanTwoCardsLeftRoundState implements the **PriorityLogic** 
     * In TwoCardsLeftRoundState: The player tries to play its weakest card which is not of trump suit.
     * In FinalRoundState: As the AI player already knows the cards of the enemy, he checks whether there are cards that he is guaranteed to take. 
	 In case he could not take a card, he checks if the enemy has trumps and if not plays a trump. Otherwise he tries to play a card from a suit that the enemy does not have.
	 If the enemy does have a trump he plays high card, otherwise he plays a weak card from that suit. Lastly if there is no such suit he just plays a small card.
1. In case he has the **second turn**:
    * Firstly, he checks whether the played enemy card is high (`A` or `10`) and then tries to take it with a trump.
    * Secondly, he checks whether he has a bigger card from the same suit a tries to take the enemy card.
    * Lastly, if nothing from above applies he throws the smallest card that is not from the trump suit.

1. Logic to **make an announce**:
   * The AI player checks for pairs of `K` and `Q` from the same suit. If the trump suit is `♦` and the cards of
    stalker player are: `[K♠ Q♠ K♦ Q♦ K♣ Q♣]` he will announce 40. Otherwise he checks for pairs from the first suit from which the
    `A` and `10` have passed and throws `K`. If negative case he plays the `Q`. _This logic is checked with priority due to its high potential (20/40 points).
	[code](https://github.com/M-Yankov/S.T.A.L.K.E.R/blob/master/SantaseEngine/Source/AI/Santase.AI.StalkerPlayer/CardHelpers/StalkerHelper.cs#L94)
 
1. **Priority** logic: [code](https://github.com/M-Yankov/S.T.A.L.K.E.R/blob/master/SantaseEngine/Source/AI/Santase.AI.StalkerPlayer/CardHelpers/CardChooser.cs)
	* The priority logic depends on the on the state of the game e.g. whether the rules should be observed or not.
	* Firstly, the AI player tries to get a priority for every suit and select a maximum:
		* The `trump priority` is the sum of all trump cards with status inStalker and Passed `(trumps inStalker+Passed)`;
		* If there are cards in left in deck one is added to the trump priority `(trumps inStalker+Passed+bottomCard)`;
		* If rules *should be observed* every other suit priority is calculated by subtracting the trump priority from the suit priority `(suit inStalker+Passed - trumpPriority)`;
		* In case the *rules should not be observed* each suits takes the trump priority except for the trump suit itself.
	* Afterwards, he checks *which suits are available* to him (whether he has cards from that suits) and `selects the suit with highest priority`.
	* If *rules should not be observed* he tries to select all cards (from all suits different from the trump suit) excluding Q or K which are waiting for announce.
	* If there are no such cards, he selects all trump cards.
	* Finally, the **decision** is made by the following two formulas:
		* If rules should not be observed he `compares the maximum priority with a predefined value` and if it is *greater* he takes the highest card from the selected cards, otherwise: the lowest.
		* In case the rules should be observed he `again compares the maximum priority with a other predefined value` and if it is *lower* he takes the highest card from the selected cards, otherwise: the lowest.
			
1. Additional details about the CardHolder class. It contains the following collections:
    
    ```CSharp
    CardSuit[] allCardSuit;
    CardType[] allCardTypes;
    IList<Card> enemyCards;
    IDictionary<CardSuit, Dictionary<CardType, CardStatus>> allCards;
    ```
    
    The access to the cards is of constant complexity and is one of the fastest way to accomplish this task. Example:
    
    ```CSharp
    this.allCards[CardSuit.Club][CardType.Ace] // gets or sets card status;
    ```
 
1. Additionally, our team has made a Pull request for bug fixing of the game[#12](https://github.com/NikolayIT/SantaseGameEngine/pull/12/files)
