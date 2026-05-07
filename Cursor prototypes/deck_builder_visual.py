import random
import tkinter as tk
from dataclasses import dataclass, field
from tkinter import messagebox
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
    effect: str
    value: int


@dataclass
class Card:
    name: str
    card_type: str
    energy_cost: int
    monster: Optional[Monster] = None
    modifier: Optional[Modifier] = None


@dataclass
class Player:
    name: str
    deck: List[Card]
    roster: List[Monster]
    hand: List[Card] = field(default_factory=list)
    discard: List[Card] = field(default_factory=list)
    active_monster: Optional[Monster] = None
    shield: int = 0
    energy: int = 3
    atk_buff: int = 0

    def draw(self, count: int = 1) -> None:
        for _ in range(count):
            if not self.deck:
                self.deck, self.discard = self.discard, []
                random.shuffle(self.deck)
            if self.deck:
                self.hand.append(self.deck.pop())


class VisualDeckBuilderGame:
    def __init__(self, root: tk.Tk) -> None:
        self.root = root
        self.root.title("Pixel Deck Builder Prototype")
        self.root.geometry("1000x720")
        self.root.configure(bg="#1f1f2e")

        self.tile = 58
        self.map_size = 7
        self.player_pos = [self.map_size // 2, self.map_size // 2]
        self.mode = "explore"  # explore | battle
        self.turn_owner = "player"
        self.last_wild: Optional[Monster] = None

        self.player = self._build_player()
        self.wild_player: Optional[Player] = None

        self._build_ui()
        self._refresh_all()

    def _build_player(self) -> Player:
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

    def _build_wild(self) -> Player:
        m = random.choice(
            [
                Monster("Mossling", 28, 9, 4, 6),
                Monster("SparkMite", 24, 10, 3, 12),
                Monster("CragCub", 34, 11, 7, 4),
                Monster("AquaNip", 30, 8, 5, 9),
            ]
        )
        deck = [
            Card(f"Summon {m.name}", "monster", 0, monster=m),
            Card("Wild Instinct", "modifier", 0, modifier=Modifier("Wild Instinct", "atk_up", 2)),
            Card("Wild Guard", "modifier", 0, modifier=Modifier("Wild Guard", "shield", 4)),
        ]
        w = Player("Wild", deck=deck, roster=[m])
        w.draw(2)
        w.active_monster = m
        return w

    def _build_ui(self) -> None:
        top = tk.Frame(self.root, bg="#1f1f2e")
        top.pack(fill="x", padx=10, pady=8)

        self.status_lbl = tk.Label(top, text="", fg="#dbeafe", bg="#1f1f2e", font=("Consolas", 11, "bold"))
        self.status_lbl.pack(side="left")

        tk.Button(top, text="Quit", command=self.root.destroy, bg="#7f1d1d", fg="white").pack(side="right")

        body = tk.Frame(self.root, bg="#1f1f2e")
        body.pack(fill="both", expand=True, padx=10, pady=8)

        left = tk.Frame(body, bg="#1f1f2e")
        left.pack(side="left", fill="y")

        self.map_canvas = tk.Canvas(
            left,
            width=self.map_size * self.tile,
            height=self.map_size * self.tile,
            bg="#111827",
            highlightthickness=0,
        )
        self.map_canvas.pack(pady=6)
        self.root.bind("<w>", lambda _e: self.move(0, -1))
        self.root.bind("<a>", lambda _e: self.move(-1, 0))
        self.root.bind("<s>", lambda _e: self.move(0, 1))
        self.root.bind("<d>", lambda _e: self.move(1, 0))

        info = tk.Label(
            left,
            text="Explore controls: W A S D\nWalk to trigger wild encounters.",
            fg="#cbd5e1",
            bg="#1f1f2e",
            justify="left",
        )
        info.pack(anchor="w", pady=(4, 0))

        right = tk.Frame(body, bg="#111827", padx=10, pady=10)
        right.pack(side="left", fill="both", expand=True, padx=(12, 0))

        self.battle_title = tk.Label(right, text="Battle Panel", fg="#f8fafc", bg="#111827", font=("Consolas", 13, "bold"))
        self.battle_title.pack(anchor="w")

        self.enemy_lbl = tk.Label(right, text="", fg="#fca5a5", bg="#111827", justify="left")
        self.enemy_lbl.pack(anchor="w", pady=(6, 0))

        self.player_lbl = tk.Label(right, text="", fg="#86efac", bg="#111827", justify="left")
        self.player_lbl.pack(anchor="w", pady=(6, 0))

        btn_row = tk.Frame(right, bg="#111827")
        btn_row.pack(fill="x", pady=10)
        self.attack_btn = tk.Button(btn_row, text="Attack", command=self.player_attack, state="disabled", bg="#be123c", fg="white")
        self.attack_btn.pack(side="left")
        self.end_turn_btn = tk.Button(
            btn_row, text="End Turn", command=self.end_turn, state="disabled", bg="#1d4ed8", fg="white"
        )
        self.end_turn_btn.pack(side="left", padx=8)

        tk.Label(right, text="Hand Cards", fg="#e2e8f0", bg="#111827", font=("Consolas", 10, "bold")).pack(anchor="w")
        self.hand_frame = tk.Frame(right, bg="#111827")
        self.hand_frame.pack(fill="x", pady=6)

        self.log = tk.Text(right, height=14, bg="#0b1220", fg="#d1fae5", insertbackground="white")
        self.log.pack(fill="both", expand=True)
        self.log.configure(state="disabled")

    def _log(self, msg: str) -> None:
        self.log.configure(state="normal")
        self.log.insert("end", f"{msg}\n")
        self.log.see("end")
        self.log.configure(state="disabled")

    def _refresh_all(self) -> None:
        self._draw_map()
        self._refresh_status()
        self._refresh_battle_panel()
        self._refresh_hand()

    def _draw_map(self) -> None:
        self.map_canvas.delete("all")
        for y in range(self.map_size):
            for x in range(self.map_size):
                x1 = x * self.tile
                y1 = y * self.tile
                x2 = x1 + self.tile
                y2 = y1 + self.tile
                terrain = random.choice(["#234e52", "#14532d", "#1f2937", "#164e63"])
                self.map_canvas.create_rectangle(x1, y1, x2, y2, fill=terrain, outline="#334155")
        px, py = self.player_pos
        self.map_canvas.create_rectangle(
            px * self.tile + 10,
            py * self.tile + 10,
            px * self.tile + self.tile - 10,
            py * self.tile + self.tile - 10,
            fill="#facc15",
            outline="#f59e0b",
            width=2,
        )
        self.map_canvas.create_text(
            px * self.tile + self.tile // 2,
            py * self.tile + self.tile // 2,
            text="P",
            fill="#111827",
            font=("Consolas", 16, "bold"),
        )

    def _refresh_status(self) -> None:
        if self.mode == "explore":
            self.status_lbl.config(text="Mode: Explore | Move with W/A/S/D")
        else:
            self.status_lbl.config(text=f"Mode: Battle | Turn: {self.turn_owner} | Energy: {self.player.energy}")

    def _monster_line(self, m: Optional[Monster], shield: int, buff: int) -> str:
        if not m:
            return "None"
        return f"{m.name}  HP {m.hp}/{m.max_hp}  ATK {m.attack}+{buff}  DEF {m.defense}  SHD {shield}"

    def _refresh_battle_panel(self) -> None:
        if self.mode != "battle" or not self.wild_player:
            self.enemy_lbl.config(text="No active battle")
            self.player_lbl.config(text="Explore to find wild monsters")
            self.attack_btn.config(state="disabled")
            self.end_turn_btn.config(state="disabled")
            return

        self.enemy_lbl.config(
            text="Enemy: " + self._monster_line(self.wild_player.active_monster, self.wild_player.shield, self.wild_player.atk_buff)
        )
        self.player_lbl.config(text="You:    " + self._monster_line(self.player.active_monster, self.player.shield, self.player.atk_buff))

        can_player_act = self.turn_owner == "player"
        self.attack_btn.config(state=("normal" if can_player_act else "disabled"))
        self.end_turn_btn.config(state=("normal" if can_player_act else "disabled"))

    def _refresh_hand(self) -> None:
        for w in self.hand_frame.winfo_children():
            w.destroy()

        if self.mode != "battle":
            tk.Label(self.hand_frame, text="Cards appear in battle.", fg="#94a3b8", bg="#111827").pack(anchor="w")
            return

        for idx, card in enumerate(self.player.hand):
            text = f"{card.name}\n[{card.card_type}] cost {card.energy_cost}"
            btn = tk.Button(
                self.hand_frame,
                text=text,
                width=18,
                height=3,
                command=lambda i=idx: self.play_card(i),
                bg="#334155",
                fg="#e2e8f0",
                wraplength=120,
            )
            btn.grid(row=0, column=idx, padx=4, pady=4, sticky="n")

    def move(self, dx: int, dy: int) -> None:
        if self.mode != "explore":
            return
        self.player_pos[0] = max(0, min(self.map_size - 1, self.player_pos[0] + dx))
        self.player_pos[1] = max(0, min(self.map_size - 1, self.player_pos[1] + dy))
        self._draw_map()

        if random.random() < 0.35:
            self.start_battle()

    def start_battle(self) -> None:
        self.mode = "battle"
        self.turn_owner = "player"
        self.wild_player = self._build_wild()
        self.last_wild = self.wild_player.roster[0]

        for m in self.player.roster:
            m.reset()
        self.player.active_monster = self._first_alive(self.player.roster)
        self.player.shield = 0
        self.player.atk_buff = 0
        self.player.energy = 3
        self.player.draw(1)

        self._log(f"A wild {self.wild_player.active_monster.name} appeared!")
        self._refresh_all()

    @staticmethod
    def _first_alive(roster: List[Monster]) -> Optional[Monster]:
        for m in roster:
            if m.alive:
                return m
        return None

    def _damage(self, attacker: Monster, atk_buff: int, defender: Monster, target_shield: int) -> int:
        raw = max(1, (attacker.attack + atk_buff) - defender.defense + random.randint(-2, 2))
        return max(0, raw - target_shield)

    def play_card(self, idx: int) -> None:
        if self.mode != "battle" or self.turn_owner != "player":
            return
        if idx < 0 or idx >= len(self.player.hand):
            return
        card = self.player.hand[idx]
        if card.energy_cost > self.player.energy:
            self._log("Not enough energy.")
            return

        if card.card_type == "monster":
            if self.player.active_monster is not None and self.player.active_monster.alive:
                self._log("You already have an active monster.")
                return
            self.player.active_monster = card.monster
            self._log(f"You summoned {card.monster.name}.")
        else:
            if not self.player.active_monster:
                self._log("Summon a monster first.")
                return
            self._apply_modifier(self.player, card.modifier)

        self.player.energy -= card.energy_cost
        self.player.discard.append(self.player.hand.pop(idx))
        self._refresh_all()

    def _apply_modifier(self, player: Player, modifier: Modifier) -> None:
        if modifier.effect == "heal" and player.active_monster:
            player.active_monster.hp = min(player.active_monster.max_hp, player.active_monster.hp + modifier.value)
            self._log(f"{player.name} healed {modifier.value}.")
        elif modifier.effect == "atk_up":
            player.atk_buff += modifier.value
            self._log(f"{player.name} attack buff +{modifier.value}.")
        elif modifier.effect == "shield":
            player.shield += modifier.value
            self._log(f"{player.name} shield +{modifier.value}.")
        elif modifier.effect == "draw":
            player.draw(modifier.value)
            self._log(f"{player.name} drew {modifier.value} card(s).")

    def player_attack(self) -> None:
        if self.mode != "battle" or self.turn_owner != "player" or not self.wild_player:
            return
        if not self.player.active_monster or not self.wild_player.active_monster:
            self._log("Both sides need active monsters.")
            return

        dmg = self._damage(
            self.player.active_monster, self.player.atk_buff, self.wild_player.active_monster, self.wild_player.shield
        )
        self.wild_player.shield = max(0, self.wild_player.shield - dmg)
        self.wild_player.active_monster.hp = max(0, self.wild_player.active_monster.hp - dmg)
        self._log(f"{self.player.active_monster.name} hits {self.wild_player.active_monster.name} for {dmg}.")
        self.player.atk_buff = 0

        if not self.wild_player.active_monster.alive:
            self._log(f"{self.wild_player.active_monster.name} fainted.")
            self.end_battle(player_won=True)
            return

        self.end_turn()

    def end_turn(self) -> None:
        if self.mode != "battle" or not self.wild_player:
            return
        if self.turn_owner != "player":
            return

        self.turn_owner = "wild"
        self.enemy_action()
        if self.mode == "battle":
            self.turn_owner = "player"
            self.player.energy = 3
            self.player.draw(1)
            self._refresh_all()

    def enemy_action(self) -> None:
        if not self.wild_player or not self.wild_player.active_monster:
            return
        wild = self.wild_player

        maybe_mod = next((c for c in wild.hand if c.card_type == "modifier"), None)
        if maybe_mod and random.random() < 0.45:
            self._apply_modifier(wild, maybe_mod.modifier)
            wild.discard.append(maybe_mod)
            wild.hand.remove(maybe_mod)

        if not self.player.active_monster:
            self.player.active_monster = self._first_alive(self.player.roster)
            if not self.player.active_monster:
                self.end_battle(player_won=False)
                return

        dmg = self._damage(wild.active_monster, wild.atk_buff, self.player.active_monster, self.player.shield)
        self.player.shield = max(0, self.player.shield - dmg)
        self.player.active_monster.hp = max(0, self.player.active_monster.hp - dmg)
        self._log(f"{wild.active_monster.name} attacks for {dmg}.")
        wild.atk_buff = 0

        if not self.player.active_monster.alive:
            self._log(f"{self.player.active_monster.name} fainted.")
            self.player.active_monster = self._first_alive(self.player.roster)
            if not self.player.active_monster:
                self.end_battle(player_won=False)

    def end_battle(self, player_won: bool) -> None:
        if player_won:
            self._log("You won the battle.")
            self.dialogue_phase()
        else:
            self._log("You lost and return to base.")
            messagebox.showinfo("Defeat", "All your monsters fainted. Returning to map center.")
            self.player_pos = [self.map_size // 2, self.map_size // 2]

        self.wild_player = None
        self.mode = "explore"
        self.turn_owner = "player"
        self._refresh_all()

    def dialogue_phase(self) -> None:
        if not self.last_wild:
            return
        qset = [
            ("A teammate is struggling. What do you do?", ["ignore", "help", "mock"], "help"),
            ("Power or strategy?", ["power", "strategy"], "strategy"),
            ("Finish quickly or safely?", ["quickly", "safely"], "safely"),
        ]
        score = 0
        for prompt, options, best in qset:
            answer = self._ask_choice(prompt, options)
            if answer == best:
                score += 1

        if score >= 2:
            m = self.last_wild
            self.player.roster.append(Monster(m.name, m.max_hp, m.attack, m.defense, m.speed))
            messagebox.showinfo("Recruitment", f"{m.name} joined your roster.")
            self._log(f"{m.name} joined your team.")
        else:
            messagebox.showinfo("Recruitment", f"{self.last_wild.name} fled.")
            self._log(f"{self.last_wild.name} fled.")

    def _ask_choice(self, prompt: str, options: List[str]) -> str:
        dialog = tk.Toplevel(self.root)
        dialog.title("Dialogue")
        dialog.grab_set()
        dialog.configure(bg="#0f172a")
        dialog.resizable(False, False)

        tk.Label(dialog, text=prompt, wraplength=420, fg="#e2e8f0", bg="#0f172a", justify="left").pack(padx=14, pady=12)

        selected = tk.StringVar(value=options[0])
        for opt in options:
            tk.Radiobutton(
                dialog,
                text=opt,
                variable=selected,
                value=opt,
                fg="#d1d5db",
                bg="#0f172a",
                selectcolor="#1e293b",
                activebackground="#0f172a",
                activeforeground="#f8fafc",
            ).pack(anchor="w", padx=20)

        tk.Button(dialog, text="Confirm", command=dialog.destroy, bg="#2563eb", fg="white").pack(pady=12)
        self.root.wait_window(dialog)
        return selected.get().strip().lower()


def main() -> None:
    root = tk.Tk()
    game = VisualDeckBuilderGame(root)
    game._log("Welcome to the visual prototype.")
    root.mainloop()


if __name__ == "__main__":
    main()
