import random
from dataclasses import dataclass, field
from typing import List, Optional


@dataclass
class Monster:
    name: str
    max_hp: int
    attack: int
    defense: int
    speed: int
    hp: int = field(init=False)

    def __post_init__(self) -> None:
        self.hp = self.max_hp

    @property
    def alive(self) -> bool:
        return self.hp > 0

    def reset(self) -> None:
        self.hp = self.max_hp


@dataclass
class Modifier:
    name: str
    effect: str  # "heal", "atk_up", "shield", "draw"
    value: int


@dataclass
class Card:
    name: str
    card_type: str  # "monster" or "modifier"
    energy_cost: int
    monster: Optional[Monster] = None
    modifier: Optional[Modifier] = None

    def playable(self, state: "BattleState") -> bool:
        if self.energy_cost > state.current_player.energy:
            return False
        if self.card_type == "monster":
            return state.current_player.active_monster is None
        if self.card_type == "modifier":
            return state.current_player.active_monster is not None
        return False


@dataclass
class Player:
    name: str
    deck: List[Card]
    hand: List[Card] = field(default_factory=list)
    discard: List[Card] = field(default_factory=list)
    roster: List[Monster] = field(default_factory=list)
    active_monster: Optional[Monster] = None
    hp_shield: int = 0
    energy: int = 3

    def draw(self, count: int = 1) -> None:
        for _ in range(count):
            if not self.deck:
                self.deck, self.discard = self.discard, []
                random.shuffle(self.deck)
            if self.deck:
                self.hand.append(self.deck.pop())

    def choose_monster_from_roster(self) -> bool:
        alive = [m for m in self.roster if m.alive]
        if not alive:
            self.active_monster = None
            return False
        self.active_monster = alive[0]
        return True


@dataclass
class BattleState:
    player: Player
    enemy: Player
    current_player: Player

    @property
    def waiting_player(self) -> Player:
        return self.enemy if self.current_player is self.player else self.player


class Game:
    def __init__(self) -> None:
        self.map_size = 7
        self.player_pos = [self.map_size // 2, self.map_size // 2]
        self.turn_count = 1
        self.player = self._build_human_player()
        self.running = True

    def _build_human_player(self) -> Player:
        roster = [
            Monster("SproutCat", 36, 12, 6, 8),
            Monster("EmberPup", 32, 14, 5, 10),
        ]
        deck = [
            Card("Summon SproutCat", "monster", 1, monster=roster[0]),
            Card("Summon EmberPup", "monster", 1, monster=roster[1]),
            Card("Quick Patch", "modifier", 1, modifier=Modifier("Quick Patch", "heal", 8)),
            Card("Battle Focus", "modifier", 1, modifier=Modifier("Battle Focus", "atk_up", 4)),
            Card("Rock Guard", "modifier", 1, modifier=Modifier("Rock Guard", "shield", 6)),
            Card("Scout Draw", "modifier", 1, modifier=Modifier("Scout Draw", "draw", 1)),
        ]
        random.shuffle(deck)
        p = Player("You", deck=deck, roster=roster)
        p.draw(4)
        return p

    def _build_wild_player(self) -> Player:
        candidates = [
            Monster("Mossling", 28, 9, 4, 6),
            Monster("SparkMite", 24, 10, 3, 12),
            Monster("CragCub", 34, 11, 7, 4),
            Monster("AquaNip", 30, 8, 5, 9),
        ]
        wild = random.choice(candidates)
        wild_card = Card(f"Summon {wild.name}", "monster", 0, monster=wild)
        mod = Card("Wild Instinct", "modifier", 0, modifier=Modifier("Wild Instinct", "atk_up", 3))
        w = Player("Wild", deck=[wild_card, mod], roster=[wild])
        w.draw(2)
        return w

    def _render_map(self) -> None:
        print("\n=== WORLD MAP ===")
        for y in range(self.map_size):
            row = []
            for x in range(self.map_size):
                if [x, y] == self.player_pos:
                    row.append("P")
                else:
                    row.append("." if random.random() > 0.1 else "~")
            print(" ".join(row))
        print("Move with WASD, Q to quit.")

    def _move_player(self, command: str) -> None:
        delta = {"w": (0, -1), "a": (-1, 0), "s": (0, 1), "d": (1, 0)}
        dx, dy = delta.get(command, (0, 0))
        self.player_pos[0] = max(0, min(self.map_size - 1, self.player_pos[0] + dx))
        self.player_pos[1] = max(0, min(self.map_size - 1, self.player_pos[1] + dy))

    def _wild_encounter_roll(self) -> bool:
        return random.random() < 0.35

    def _damage(self, attacker: Monster, defender: Monster, shield: int = 0) -> int:
        raw = max(1, attacker.attack - defender.defense + random.randint(-2, 2))
        dealt = max(0, raw - shield)
        defender.hp = max(0, defender.hp - dealt)
        return dealt

    def _apply_modifier(self, player: Player, modifier: Modifier) -> None:
        if not player.active_monster:
            return
        if modifier.effect == "heal":
            player.active_monster.hp = min(player.active_monster.max_hp, player.active_monster.hp + modifier.value)
            print(f"{player.name} healed {modifier.value} HP.")
        elif modifier.effect == "atk_up":
            player.active_monster.attack += modifier.value
            print(f"{player.name}'s attack rose by {modifier.value}.")
        elif modifier.effect == "shield":
            player.hp_shield += modifier.value
            print(f"{player.name} gained {modifier.value} shield.")
        elif modifier.effect == "draw":
            player.draw(modifier.value)
            print(f"{player.name} drew {modifier.value} card(s).")

    def _play_card(self, state: BattleState, card_idx: int) -> bool:
        p = state.current_player
        if card_idx < 0 or card_idx >= len(p.hand):
            return False
        card = p.hand[card_idx]
        if not card.playable(state):
            return False
        p.energy -= card.energy_cost
        p.hand.pop(card_idx)
        p.discard.append(card)
        if card.card_type == "monster" and card.monster:
            p.active_monster = card.monster
            print(f"{p.name} summoned {p.active_monster.name}.")
        elif card.card_type == "modifier" and card.modifier:
            self._apply_modifier(p, card.modifier)
        return True

    def _player_turn(self, state: BattleState) -> None:
        p = state.current_player
        e = state.waiting_player
        p.energy = 3
        p.draw(1)
        while True:
            print(f"\n{p.name} turn | Energy: {p.energy}")
            print(f"Active: {p.active_monster.name if p.active_monster else 'None'}")
            print(f"Enemy: {e.active_monster.name if e.active_monster else 'None'}")
            for i, card in enumerate(p.hand):
                print(f"{i+1}. {card.name} [{card.card_type}] cost:{card.energy_cost}")
            print("A. Attack   E. End turn")
            cmd = input("> ").strip().lower()
            if cmd == "e":
                break
            if cmd == "a":
                if not p.active_monster or not e.active_monster:
                    print("Need active monsters to attack.")
                    continue
                dmg = self._damage(p.active_monster, e.active_monster, e.hp_shield)
                e.hp_shield = max(0, e.hp_shield - dmg)
                print(f"{p.active_monster.name} dealt {dmg} damage to {e.active_monster.name}.")
                if not e.active_monster.alive:
                    print(f"{e.active_monster.name} fainted!")
                    if not e.choose_monster_from_roster():
                        return
                break
            if cmd.isdigit():
                if not self._play_card(state, int(cmd) - 1):
                    print("Cannot play that card now.")
            else:
                print("Invalid action.")

    def _enemy_turn(self, state: BattleState) -> None:
        p = state.current_player
        e = state.waiting_player
        p.energy = 3
        p.draw(1)
        played = False
        for i, card in enumerate(list(p.hand)):
            if card.playable(state):
                self._play_card(state, i)
                played = True
                break
        if p.active_monster and e.active_monster:
            dmg = self._damage(p.active_monster, e.active_monster, e.hp_shield)
            e.hp_shield = max(0, e.hp_shield - dmg)
            print(f"{p.active_monster.name} hits {e.active_monster.name} for {dmg}.")
            if not e.active_monster.alive:
                print(f"{e.active_monster.name} fainted!")
                e.choose_monster_from_roster()
        elif not played:
            print("Wild monster hesitates.")

    def _battle(self, wild_player: Player) -> bool:
        for m in self.player.roster:
            m.reset()
        for m in wild_player.roster:
            m.reset()

        self.player.active_monster = None
        self.player.hp_shield = 0
        wild_player.active_monster = wild_player.roster[0]
        wild_player.hp_shield = 0

        state = BattleState(player=self.player, enemy=wild_player, current_player=self.player)

        print("\nA wild monster appears!")
        while True:
            if not self.player.choose_monster_from_roster():
                print("All your monsters fainted. You black out and return to base.")
                return False
            if not wild_player.choose_monster_from_roster():
                print("Wild monster defeated!")
                return True

            if self.player.active_monster.speed >= wild_player.active_monster.speed:
                order = [self.player, wild_player]
            else:
                order = [wild_player, self.player]

            for actor in order:
                if not self.player.choose_monster_from_roster():
                    return False
                if not wild_player.choose_monster_from_roster():
                    return True
                state.current_player = actor
                if actor is self.player:
                    self._player_turn(state)
                else:
                    self._enemy_turn(state)

    def _dialogue_phase(self, wild: Monster) -> None:
        print("\nThe defeated monster stares at you...")
        questions = [
            ("A teammate is struggling. What do you do?", ["ignore", "help", "mock"], "help"),
            ("Power or strategy?", ["power", "strategy"], "strategy"),
            ("Finish the battle quickly or safely?", ["quickly", "safely"], "safely"),
        ]
        score = 0
        for q, options, good in questions:
            print(f"\n{q}")
            print("Options:", ", ".join(options))
            answer = input("> ").strip().lower()
            if answer == good:
                score += 1
                print("The monster seems impressed.")
            else:
                print("The monster looks uncertain.")
        if score >= 2:
            print(f"{wild.name} joins your roster!")
            self.player.roster.append(Monster(wild.name, wild.max_hp, wild.attack, wild.defense, wild.speed))
        else:
            print(f"{wild.name} flees into the tall grass.")

    def game_loop(self) -> None:
        print("Pixel Deck Builder Prototype")
        print("- Explore map")
        print("- Trigger wild encounters")
        print("- Use card deck in turn battles")

        while self.running:
            self._render_map()
            cmd = input("\nMove (W/A/S/D) or Q: ").strip().lower()
            if cmd == "q":
                self.running = False
                break
            if cmd not in {"w", "a", "s", "d"}:
                print("Invalid move.")
                continue

            self._move_player(cmd)
            self.turn_count += 1
            if self._wild_encounter_roll():
                wild_player = self._build_wild_player()
                wild_monster = wild_player.roster[0]
                won = self._battle(wild_player)
                if won:
                    self._dialogue_phase(wild_monster)

        print("\nThanks for playing prototype build.")


if __name__ == "__main__":
    random.seed()
    Game().game_loop()
