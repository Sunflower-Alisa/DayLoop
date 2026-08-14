import time


class Metrics:
    def __init__(self) -> None:
        self._counters: dict[str, int] = {}
        self._timings: dict[str, list[float]] = {}

    def inc(self, name: str, delta: int = 1) -> None:
        self._counters[name] = self._counters.get(name, 0) + delta

    def timeit(self, name: str):
        return _Timer(self, name)

    def snapshot(self) -> dict:
        return {
            "counters": dict(self._counters),
            "timings": {k: sum(v) / len(v) if v else 0 for k, v in self._timings.items()},
        }


class _Timer:
    def __init__(self, metrics: Metrics, name: str) -> None:
        self._metrics = metrics
        self._name = name

    def __enter__(self) -> None:
        self._start = time.perf_counter()

    def __exit__(self, *args) -> None:
        elapsed = time.perf_counter() - self._start
        self._metrics._timings.setdefault(self._name, []).append(elapsed)


metrics = Metrics()
