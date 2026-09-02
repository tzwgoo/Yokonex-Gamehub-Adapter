import unittest

from bridge import normalize_mod_event


class NormalizeModEventTests(unittest.TestCase):
    def test_reads_pascal_case_event_written_by_csharp_mod(self) -> None:
        result = normalize_mod_event({
            "kind": "event",
            "type": "player.damaged",
            "data": {
                "EventId": "event-1",
                "RunId": "run-1",
                "Floor": 7,
                "RoomType": "combat",
                "Payload": {"amount": 6, "health": 42, "maxHealth": 80},
            },
        })

        self.assertEqual(
            result,
            (
                "slay_the_spire_2.player_damaged",
                {
                    "amount": 6,
                    "health": 42,
                    "maxHealth": 80,
                    "upstreamEventId": "event-1",
                    "runId": "run-1",
                    "floor": 7,
                    "roomType": "combat",
                },
            ),
        )

    def test_keeps_camel_case_compatibility(self) -> None:
        result = normalize_mod_event({
            "kind": "event",
            "type": "item.purchased",
            "data": {
                "eventId": "event-2",
                "runId": "run-2",
                "floor": 8,
                "roomType": "shop",
                "payload": {"id": "RELIC.TEST", "goldSpent": 120},
            },
        })

        self.assertEqual(
            result,
            (
                "slay_the_spire_2.item_purchased",
                {
                    "id": "RELIC.TEST",
                    "goldSpent": 120,
                    "upstreamEventId": "event-2",
                    "runId": "run-2",
                    "floor": 8,
                    "roomType": "shop",
                },
            ),
        )


if __name__ == "__main__":
    unittest.main()
