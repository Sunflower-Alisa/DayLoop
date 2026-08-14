from abc import ABC, abstractmethod


class BaseAgent(ABC):
    name: str = "base"

    @abstractmethod
    def run(self, state: dict) -> dict:
        raise NotImplementedError
