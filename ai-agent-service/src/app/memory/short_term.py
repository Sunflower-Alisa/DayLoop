class ShortTermMemory:
    def __init__(self, max_turns: int = 10) -> None:
        self.max_turns = max_turns
        self._turns: list[dict] = []

    def add(self, role: str, content: str) -> None:
        self._turns.append({"role": role, "content": content})
        if len(self._turns) > self.max_turns:
            self._turns.pop(0)

    def get(self) -> list[dict]:
        return list(self._turns)
