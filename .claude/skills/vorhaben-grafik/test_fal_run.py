"""Tests for fal_run.py. No network: urllib.request.urlopen, the environment and winreg are replaced."""

from __future__ import annotations

import base64
import contextlib
import email.message
import http.client
import io
import json
import sys
import urllib.error
import urllib.request
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent))

import fal_run  # noqa: E402

SECRET = "test-key-7f3a9c"
ENDPOINT = "fal-ai/nano-banana-pro/edit"
SUBMIT_URL = f"https://queue.fal.run/{ENDPOINT}"
STATUS_URL = "https://queue.fal.run/fal-ai/nano-banana-pro/requests/req-123/status"
RESPONSE_URL = "https://queue.fal.run/fal-ai/nano-banana-pro/requests/req-123"
CDN_URL = "https://v3b.fal.media/files/b/abc/image-1.png"
PRICING_URL = f"https://api.fal.ai/v1/models/pricing?endpoint_id={ENDPOINT}"
PNG_BYTES = b"\x89PNG\r\n\x1a\n" + b"generated"
SUBMITTED = {"request_id": "req-123", "status_url": STATUS_URL, "response_url": RESPONSE_URL}


class FakeResponse:
    def __init__(self, body: bytes):
        self._body = body

    def read(self) -> bytes:
        return self._body

    def __enter__(self):
        return self

    def __exit__(self, *exc):
        return False


class FakeNetwork:
    """Stands in for urlopen. Answers by (method, URL); the last answer of a list repeats."""

    def __init__(self, routes: dict):
        self.routes = {key: list(value) for key, value in routes.items()}
        self.calls: list[urllib.request.Request] = []

    def __call__(self, request, timeout=None):
        self.calls.append(request)
        answers = self.routes.get((request.get_method(), request.full_url))
        if not answers:
            raise AssertionError(f"unexpected request {request.get_method()} {request.full_url}")
        answer = answers.pop(0) if len(answers) > 1 else answers[0]
        if isinstance(answer, BaseException):
            raise answer
        if isinstance(answer, (dict, list)):
            answer = json.dumps(answer).encode("utf-8")
        return FakeResponse(answer)

    def calls_to(self, url: str) -> list[urllib.request.Request]:
        return [call for call in self.calls if call.full_url == url]


class FakeWinreg:
    HKEY_CURRENT_USER = object()
    REG_SZ = 1
    REG_EXPAND_SZ = 2

    def __init__(self, values: dict):
        self.values = values
        self.queried: list[str] = []

    def OpenKey(self, root, path):
        assert root is self.HKEY_CURRENT_USER
        assert path == "Environment"
        return contextlib.nullcontext(path)

    def QueryValueEx(self, handle, name):
        self.queried.append(name)
        if name not in self.values:
            raise FileNotFoundError(2, "The system cannot find the file specified")
        return self.values[name], self.REG_SZ


def http_error(url: str, code: int, body: str, headers: dict | None = None) -> urllib.error.HTTPError:
    message = email.message.Message()
    for name, value in (headers or {}).items():
        message[name] = value
    return urllib.error.HTTPError(url, code, "error", message, io.BytesIO(body.encode("utf-8")))


def headers_of(request: urllib.request.Request) -> dict[str, str]:
    return {name.lower(): value for name, value in request.header_items()}


@pytest.fixture(autouse=True)
def isolated(monkeypatch):
    """No key, no registry, no network and no waiting unless a test sets them up."""
    monkeypatch.delenv("FAL_KEY", raising=False)
    monkeypatch.setattr(fal_run, "winreg", None)
    monkeypatch.setattr(urllib.request, "urlopen", FakeNetwork({}))
    sleeps: list[float] = []
    monkeypatch.setattr(fal_run, "_sleep", sleeps.append)
    return sleeps


@pytest.fixture
def key_in_env(monkeypatch):
    monkeypatch.setenv("FAL_KEY", SECRET)


def use_network(monkeypatch, routes: dict) -> FakeNetwork:
    network = FakeNetwork(routes)
    monkeypatch.setattr(urllib.request, "urlopen", network)
    return network


DEFAULT_INPUT = {"prompt": "A sword", "num_images": 1, "resolution": "1K"}


def write_input(tmp_path: Path, payload: dict | None = None, name: str = "input.json") -> Path:
    """The request input; payload is merged over DEFAULT_INPUT, a value of None drops the field."""
    merged = {**DEFAULT_INPUT, **(payload or {})}
    path = tmp_path / name
    path.write_text(json.dumps({key: value for key, value in merged.items() if value is not None}), encoding="utf-8")
    return path


def run_args(tmp_path: Path, *extra: str, out_dir: Path | None = None, input_path: Path | None = None) -> list[str]:
    """A run command with tmp_path as the run directory and tmp_path/out as the output directory."""
    return [
        "run", ENDPOINT,
        "--json", str(input_path or write_input(tmp_path)),
        "--run-dir", str(tmp_path),
        "--out", str(out_dir or tmp_path / "out"),
        *extra,
    ]


def write_spent(directory: Path, estimated_usd: float, num_images: int = 1, state: str = "completed") -> Path:
    """A request.json of an earlier request in the run, as run writes it."""
    directory.mkdir(parents=True, exist_ok=True)
    path = directory / "request.json"
    cost = {"num_images": num_images, "unit_price_usd": estimated_usd / num_images, "estimated_usd": estimated_usd,
            "price_source": "known price list"}
    path.write_text(json.dumps({"request_id": f"req-{directory.name}", "state": state, "cost": cost}),
                    encoding="utf-8")
    return path


def assert_secret_absent(capsys, *paths: Path) -> tuple[str, str]:
    out, err = capsys.readouterr()
    assert SECRET not in out
    assert SECRET not in err
    for path in paths:
        if path.exists():
            assert SECRET not in path.read_text(encoding="utf-8")
    return out, err


# --- check ----------------------------------------------------------------------------------------------------------


def test_check_finds_key_in_process_environment_without_printing_it(key_in_env, capsys):
    assert fal_run.main(["check"]) == 0
    out, _ = assert_secret_absent(capsys)
    assert "found in the process environment" in out


def test_check_reads_user_environment_from_registry(monkeypatch, capsys):
    registry = FakeWinreg({"FAL_KEY": SECRET})
    monkeypatch.setattr(fal_run, "winreg", registry)

    assert fal_run.main(["check"]) == 0

    out, _ = assert_secret_absent(capsys)
    assert "HKCU\\Environment" in out
    assert "older value" not in out
    assert registry.queried == ["FAL_KEY"]


def test_user_environment_wins_over_stale_process_value(monkeypatch, capsys):
    # The app inherited the old key at its start; Marcus has replaced it in the user variable since.
    monkeypatch.setenv("FAL_KEY", "stale-key")
    monkeypatch.setattr(fal_run, "winreg", FakeWinreg({"FAL_KEY": SECRET}))

    assert fal_run.find_key() == (SECRET, "HKCU\\Environment")
    assert fal_run.main(["check"]) == 0

    out, err = assert_secret_absent(capsys)
    assert "older value" in out
    assert "stale-key" not in out
    assert "stale-key" not in err


def test_user_environment_key_is_sent_instead_of_the_stale_process_value(monkeypatch, capsys):
    monkeypatch.setenv("FAL_KEY", "stale-key")
    monkeypatch.setattr(fal_run, "winreg", FakeWinreg({"FAL_KEY": SECRET}))
    network = use_network(monkeypatch, {("GET", PRICING_URL): [{"prices": [{"unit_price": 0.15}]}]})

    assert fal_run.main(["price", ENDPOINT]) == 0

    (call,) = network.calls
    assert headers_of(call)["authorization"] == f"Key {SECRET}"
    assert_secret_absent(capsys)


def test_check_without_key_fails_and_explains_setup(monkeypatch, capsys):
    registry = FakeWinreg({"TRIPO_API_KEY": "other"})
    monkeypatch.setattr(fal_run, "winreg", registry)

    assert fal_run.main(["check"]) == 1

    _, err = capsys.readouterr()
    assert "FAL_KEY" in err
    assert "restart" in err
    assert registry.queried == ["FAL_KEY"]


def test_blank_process_variable_does_not_count_as_key(monkeypatch):
    monkeypatch.setenv("FAL_KEY", "   ")
    assert fal_run.find_key() == (None, None)


def test_key_with_a_line_break_is_refused_before_any_request(monkeypatch, capsys):
    monkeypatch.setenv("FAL_KEY", f"{SECRET}\nsecond-half")
    network = use_network(monkeypatch, {})

    assert fal_run.main(["price", ENDPOINT]) == 1

    assert network.calls == []
    _, err = assert_secret_absent(capsys)
    assert "second-half" not in err
    assert "FAL_KEY in the process environment" in err


def test_check_reports_a_key_with_a_line_break_as_unusable(monkeypatch, capsys):
    monkeypatch.setattr(fal_run, "winreg", FakeWinreg({"FAL_KEY": f"{SECRET}\r\nsecond-half"}))

    assert fal_run.main(["check"]) == 1

    _, err = assert_secret_absent(capsys)
    assert "second-half" not in err
    assert "FAL_KEY in HKCU\\Environment" in err


# --- price ----------------------------------------------------------------------------------------------------------


def test_price_sends_key_only_in_header_and_prints_unit_price(key_in_env, monkeypatch, capsys):
    price = {"endpoint_id": ENDPOINT, "unit_price": 0.15, "unit": "image", "currency": "USD"}
    network = use_network(monkeypatch, {("GET", PRICING_URL): [{"prices": [price], "has_more": False}]})

    assert fal_run.main(["price", ENDPOINT]) == 0

    (call,) = network.calls
    assert headers_of(call)["authorization"] == f"Key {SECRET}"
    # Unredirected: urllib does not copy it into a redirected request.
    assert call.unredirected_hdrs["Authorization"] == f"Key {SECRET}"
    assert "Authorization" not in call.headers
    assert SECRET not in call.full_url
    out, _ = assert_secret_absent(capsys)
    assert json.loads(out) == [price]


def test_price_for_unknown_endpoint_fails(key_in_env, monkeypatch, capsys):
    url = "https://api.fal.ai/v1/models/pricing?endpoint_id=fal-ai/unknown"
    use_network(monkeypatch, {("GET", url): [{"prices": []}]})

    assert fal_run.main(["price", "fal-ai/unknown"]) == 1
    assert "No price listed" in capsys.readouterr().err


# --- run ------------------------------------------------------------------------------------------------------------


def completed_routes(response: dict | None = None) -> dict:
    return {
        ("GET", STATUS_URL): [{"status": "IN_QUEUE"}, {"status": "IN_PROGRESS"}, {"status": "COMPLETED"}],
        ("GET", RESPONSE_URL): [response or {
            "images": [{"url": CDN_URL, "content_type": "image/png", "width": 1024, "height": 1024}],
            "seed": 42,
            "description": "",
        }],
        ("GET", CDN_URL): [PNG_BYTES],
    }


def test_run_queues_waits_downloads_and_writes_result(key_in_env, monkeypatch, tmp_path, capsys):
    reference = tmp_path / "crop.png"
    reference.write_bytes(b"reference-bytes")
    input_path = write_input(tmp_path, {"image_urls": ["https://example.org/a.png"]})
    out_dir = tmp_path / "out"
    network = use_network(monkeypatch, {("POST", SUBMIT_URL): [SUBMITTED], **completed_routes()})

    code = fal_run.main(run_args(tmp_path, "--image", str(reference), input_path=input_path))

    assert code == 0
    (submit,) = network.calls_to(SUBMIT_URL)
    headers = headers_of(submit)
    assert headers["authorization"] == f"Key {SECRET}"
    assert headers["content-type"] == "application/json"
    assert headers["x-fal-store-io"] == "0"
    assert json.loads(headers["x-fal-object-lifecycle-preference"]) == {"expiration_duration_seconds": 86400}
    body = json.loads(submit.data)
    expected_uri = "data:image/png;base64," + base64.b64encode(b"reference-bytes").decode("ascii")
    assert body["image_urls"] == ["https://example.org/a.png", expected_uri]
    assert len(network.calls_to(STATUS_URL)) == 3
    assert all(headers_of(call)["authorization"] == f"Key {SECRET}" for call in network.calls_to(STATUS_URL))
    (download,) = network.calls_to(CDN_URL)
    assert "authorization" not in headers_of(download)

    assert (out_dir / "req-123-1.png").read_bytes() == PNG_BYTES
    result = json.loads((out_dir / "result.json").read_text(encoding="utf-8"))
    assert result["request_id"] == "req-123"
    assert result["endpoint"] == ENDPOINT
    assert result["seed"] == 42
    assert result["outputs"] == [
        {"file": "req-123-1.png", "url": CDN_URL, "content_type": "image/png", "width": 1024, "height": 1024}
    ]
    assert result["input"]["image_urls"] == ["https://example.org/a.png", f"file:{reference}"]
    assert "description" not in result
    request = json.loads((out_dir / "request.json").read_text(encoding="utf-8"))
    assert request["state"] == "completed"
    assert request["request_id"] == "req-123"
    out, _ = assert_secret_absent(capsys, out_dir / "result.json", out_dir / "request.json")
    assert str(out_dir / "result.json") in out


def test_run_keeps_input_seed_when_response_has_none(key_in_env, monkeypatch, tmp_path):
    input_path = write_input(tmp_path, {"seed": 7})
    response = {"images": [{"url": CDN_URL}]}
    use_network(monkeypatch, {("POST", SUBMIT_URL): [SUBMITTED], **completed_routes(response)})

    assert fal_run.main(run_args(tmp_path, input_path=input_path)) == 0

    result = json.loads((tmp_path / "out" / "result.json").read_text(encoding="utf-8"))
    assert result["seed"] == 7
    assert result["outputs"][0]["file"] == "req-123-1.png"


def test_run_saves_inline_data_uri_images(key_in_env, monkeypatch, tmp_path):
    inline = "data:image/webp;base64," + base64.b64encode(b"webp-bytes").decode("ascii")
    use_network(monkeypatch, {("POST", SUBMIT_URL): [SUBMITTED], **completed_routes({"images": [{"url": inline}]})})

    assert fal_run.main(run_args(tmp_path)) == 0

    assert (tmp_path / "out" / "req-123-1.webp").read_bytes() == b"webp-bytes"


def test_run_refuses_used_output_directory_before_any_request(key_in_env, monkeypatch, tmp_path, capsys):
    out_dir = tmp_path / "out"
    out_dir.mkdir()
    (out_dir / "result.json").write_text("{}", encoding="utf-8")
    network = use_network(monkeypatch, {})

    assert fal_run.main(run_args(tmp_path, out_dir=out_dir)) == 1

    assert network.calls == []
    assert "fresh --out directory" in capsys.readouterr().err


def test_run_on_unfinished_request_points_to_resume_not_to_a_new_request(key_in_env, monkeypatch, tmp_path, capsys):
    # A run killed after queueing (tool timeout, Ctrl+C) leaves request.json without result.json.
    out_dir = tmp_path / "out"
    out_dir.mkdir()
    (out_dir / "request.json").write_text(json.dumps({**SUBMITTED, "endpoint": ENDPOINT}), encoding="utf-8")
    network = use_network(monkeypatch, {})

    assert fal_run.main(run_args(tmp_path, out_dir=out_dir)) == 1

    assert network.calls == []
    err = capsys.readouterr().err
    assert "req-123" in err
    assert f"resume --out {out_dir}" in err
    assert "fresh --out directory" not in err

    network = use_network(monkeypatch, completed_routes())
    assert fal_run.main(["resume", "--out", str(out_dir)]) == 0
    assert network.calls_to(SUBMIT_URL) == []


def test_run_without_key_sends_nothing(monkeypatch, tmp_path, capsys):
    network = use_network(monkeypatch, {})

    assert fal_run.main(run_args(tmp_path)) == 1

    assert network.calls == []
    assert "FAL_KEY" in capsys.readouterr().err
    assert not (tmp_path / "out" / "request.json").exists()


def test_run_rejects_unsupported_reference_image(key_in_env, monkeypatch, tmp_path, capsys):
    reference = tmp_path / "crop.gif"
    reference.write_bytes(b"gif")
    network = use_network(monkeypatch, {})

    code = fal_run.main(run_args(tmp_path, "--image", str(reference)))

    assert code == 1
    assert network.calls == []
    assert "PNG, JPEG and WebP" in capsys.readouterr().err
    assert not (tmp_path / "out" / "request.json").exists()


def test_run_refuses_output_directory_outside_the_run(key_in_env, monkeypatch, tmp_path, capsys):
    network = use_network(monkeypatch, {})
    run_dir = tmp_path / "run"
    run_dir.mkdir()

    code = fal_run.main(["run", ENDPOINT, "--json", str(write_input(tmp_path)), "--run-dir", str(run_dir),
                         "--out", str(tmp_path / "elsewhere")])

    assert code == 1
    assert network.calls == []
    assert "must lie inside --run-dir" in capsys.readouterr().err


# --- cost and budget ------------------------------------------------------------------------------------------------


@pytest.mark.parametrize(
    ("resolution", "num_images", "unit_price", "estimated"),
    [("1K", 3, 0.15, 0.45), ("2K", 2, 0.15, 0.3), ("4K", 2, 0.30, 0.6)],
)
def test_run_records_the_planned_cost_in_request_json(resolution, num_images, unit_price, estimated, key_in_env,
                                                      monkeypatch, tmp_path):
    input_path = write_input(tmp_path, {"resolution": resolution, "num_images": num_images})
    use_network(monkeypatch, {("POST", SUBMIT_URL): [SUBMITTED], **completed_routes()})

    assert fal_run.main(run_args(tmp_path, input_path=input_path)) == 0

    cost = json.loads((tmp_path / "out" / "request.json").read_text(encoding="utf-8"))["cost"]
    assert cost == {"num_images": num_images, "unit_price_usd": unit_price, "estimated_usd": estimated,
                    "price_source": "known price list"}


def test_run_takes_the_unit_price_of_another_endpoint_from_the_option(key_in_env, monkeypatch, tmp_path):
    other = "fal-ai/other/edit"
    submit_url = f"https://queue.fal.run/{other}"
    use_network(monkeypatch, {("POST", submit_url): [SUBMITTED], **completed_routes()})
    input_path = write_input(tmp_path, {"num_images": 2, "resolution": None})

    code = fal_run.main(["run", other, "--json", str(input_path), "--run-dir", str(tmp_path),
                         "--out", str(tmp_path / "out"), "--unit-price", "0.04"])

    assert code == 0
    cost = json.loads((tmp_path / "out" / "request.json").read_text(encoding="utf-8"))["cost"]
    assert cost["estimated_usd"] == 0.08
    assert cost["price_source"] == "--unit-price"


@pytest.mark.parametrize(
    ("endpoint", "payload", "message"),
    [
        ("fal-ai/other/edit", {}, "--unit-price"),  # no known price for this endpoint
        (ENDPOINT, {"resolution": None}, "--unit-price"),  # resolution missing
        (ENDPOINT, {"resolution": "8K"}, "--unit-price"),  # resolution without a known price
        (ENDPOINT, {"num_images": None}, "num_images"),  # count missing
        (ENDPOINT, {"num_images": 0}, "num_images"),
        (ENDPOINT, {"resolution": ["1K"]}, "--unit-price"),  # resolution not a string
        (ENDPOINT, {"enable_web_search": True}, "enable_web_search"),  # per-request surcharge the estimate cannot count
    ],
)
def test_run_without_a_known_cost_sends_nothing(endpoint, payload, message, key_in_env, monkeypatch, tmp_path, capsys):
    network = use_network(monkeypatch, {})

    code = fal_run.main(["run", endpoint, "--json", str(write_input(tmp_path, payload)), "--run-dir", str(tmp_path),
                         "--out", str(tmp_path / "out")])

    assert code == 1
    assert network.calls == []
    assert message in capsys.readouterr().err
    assert not (tmp_path / "out" / "request.json").exists()


@pytest.mark.parametrize(
    ("payload", "unit_price", "floor"),
    [
        ({"num_images": 4, "resolution": "4K"}, "0.15", "0.3 USD"),  # 'price' reports only the base price
        ({"num_images": 4, "resolution": None}, "0.15", "0.3 USD"),  # no resolution: the highest list price counts
    ],
)
def test_run_refuses_a_unit_price_below_the_known_list_price(payload, unit_price, floor, key_in_env, monkeypatch,
                                                             tmp_path, capsys):
    network = use_network(monkeypatch, {})
    input_path = write_input(tmp_path, payload)

    code = fal_run.main(run_args(tmp_path, "--unit-price", unit_price, input_path=input_path))

    assert code == 1
    assert network.calls == []
    err = capsys.readouterr().err
    assert "below the list price" in err
    assert floor in err
    assert not (tmp_path / "out" / "request.json").exists()


def test_run_takes_a_unit_price_above_the_known_list_price(key_in_env, monkeypatch, tmp_path):
    use_network(monkeypatch, {("POST", SUBMIT_URL): [SUBMITTED], **completed_routes()})
    input_path = write_input(tmp_path, {"num_images": 2, "resolution": "1K"})

    assert fal_run.main(run_args(tmp_path, "--unit-price", "0.2", input_path=input_path)) == 0

    cost = json.loads((tmp_path / "out" / "request.json").read_text(encoding="utf-8"))["cost"]
    assert cost["estimated_usd"] == 0.4
    assert cost["price_source"] == "--unit-price"


@pytest.mark.parametrize(
    ("option", "value"),
    [("--unit-price", "nan"), ("--unit-price", "inf"), ("--budget", "nan"), ("--budget", "inf"),
     ("--spent-before", "nan"), ("--spent-before", "-1")],
)
def test_run_refuses_an_amount_that_is_not_finite_or_negative(option, value, key_in_env, monkeypatch, tmp_path,
                                                             capsys):
    network = use_network(monkeypatch, {("POST", SUBMIT_URL): [SUBMITTED], **completed_routes()})
    input_path = write_input(tmp_path, {"num_images": 4})

    code = fal_run.main(run_args(tmp_path, option, value, input_path=input_path))

    assert code == 1
    assert network.calls == []
    assert f"{option} must be a finite amount" in capsys.readouterr().err
    assert not (tmp_path / "out" / "request.json").exists()


@pytest.mark.parametrize("estimated", ["NaN", "Infinity", -10])
def test_request_with_a_cost_that_is_not_finite_or_negative_stops_the_count(estimated, key_in_env, monkeypatch,
                                                                            tmp_path, capsys):
    earlier = tmp_path / "icon-a" / "1"
    earlier.mkdir(parents=True)
    cost = {"num_images": 1, "unit_price_usd": 0.15, "estimated_usd": estimated, "price_source": "known price list"}
    (earlier / "request.json").write_text(json.dumps({"request_id": "req-a", "state": "completed", "cost": cost}),
                                          encoding="utf-8")
    network = use_network(monkeypatch, {("POST", SUBMIT_URL): [SUBMITTED], **completed_routes()})

    assert fal_run.main(run_args(tmp_path)) == 1

    assert network.calls == []
    assert "holds no readable cost (ValueError)" in capsys.readouterr().err
    assert not (tmp_path / "out" / "request.json").exists()
    assert fal_run.main(["cost", "--run-dir", str(tmp_path)]) == 1
    assert "holds no readable cost" in capsys.readouterr().err


def test_run_over_budget_sends_nothing_and_ends_with_5(key_in_env, monkeypatch, tmp_path, capsys):
    write_spent(tmp_path / "icon-a" / "1", 1.50, num_images=10)
    write_spent(tmp_path / "icon-b" / "1", 1.20, num_images=8)
    network = use_network(monkeypatch, {})
    input_path = write_input(tmp_path, {"num_images": 3})

    assert fal_run.main(run_args(tmp_path, input_path=input_path)) == 5

    assert network.calls == []
    assert not (tmp_path / "out" / "request.json").exists()
    err = capsys.readouterr().err
    assert "has spent 2.70 USD" in err
    assert "would add 0.45 USD" in err
    assert "limit is 3.00 USD" in err
    assert "Ask Marcus" in err


def test_run_counts_earlier_runs_on_the_same_pr(key_in_env, monkeypatch, tmp_path, capsys):
    network = use_network(monkeypatch, {})
    input_path = write_input(tmp_path, {"num_images": 3})

    assert fal_run.main(run_args(tmp_path, "--spent-before", "2.70", input_path=input_path)) == 5

    assert network.calls == []
    assert "2.70 USD in earlier runs" in capsys.readouterr().err


def test_run_within_a_raised_budget_goes_through(key_in_env, monkeypatch, tmp_path):
    write_spent(tmp_path / "icon-a" / "1", 2.70, num_images=18)
    network = use_network(monkeypatch, {("POST", SUBMIT_URL): [SUBMITTED], **completed_routes()})
    input_path = write_input(tmp_path, {"num_images": 3})

    assert fal_run.main(run_args(tmp_path, "--budget", "5", input_path=input_path)) == 0

    assert len(network.calls_to(SUBMIT_URL)) == 1


def test_run_up_to_exactly_the_budget_goes_through(key_in_env, monkeypatch, tmp_path):
    write_spent(tmp_path / "icon-a" / "1", 2.55, num_images=17)
    use_network(monkeypatch, {("POST", SUBMIT_URL): [SUBMITTED], **completed_routes()})

    assert fal_run.main(run_args(tmp_path, input_path=write_input(tmp_path, {"num_images": 3}))) == 0


def test_network_error_on_submit_keeps_the_reservation_and_counts_it(key_in_env, monkeypatch, tmp_path, capsys):
    use_network(monkeypatch, {("POST", SUBMIT_URL): [urllib.error.URLError("connection reset")]})
    out_dir = tmp_path / "out"

    assert fal_run.main(run_args(tmp_path, out_dir=out_dir, input_path=write_input(tmp_path, {"num_images": 2}))) == 1

    request = json.loads((out_dir / "request.json").read_text(encoding="utf-8"))
    assert request["state"] == "submitting"
    assert request["request_id"] is None
    assert request["cost"]["estimated_usd"] == 0.3
    err = capsys.readouterr().err
    assert "Whether fal.ai queued the request is unknown" in err
    assert "resume --out" not in err

    assert fal_run.main(["cost", "--run-dir", str(tmp_path)]) == 0
    assert json.loads(capsys.readouterr().out)["estimated_usd"] == 0.3

    # The same directory is refused, and resume has nothing to fetch.
    assert fal_run.main(run_args(tmp_path, out_dir=out_dir)) == 1
    assert "got no answer" in capsys.readouterr().err
    assert fal_run.main(["resume", "--out", str(out_dir)]) == 1
    assert "nothing to resume" in capsys.readouterr().err


@pytest.mark.parametrize("code", [500, 502, 503, 504])
def test_server_error_on_submit_keeps_the_reservation_and_counts_it(code, key_in_env, monkeypatch, tmp_path, capsys):
    # A gateway may time out after the queue has accepted the request: it may be queued and paid for.
    use_network(monkeypatch, {("POST", SUBMIT_URL): [http_error(SUBMIT_URL, code, "gateway timeout")]})
    out_dir = tmp_path / "out"

    assert fal_run.main(run_args(tmp_path, out_dir=out_dir, input_path=write_input(tmp_path, {"num_images": 2}))) == 1

    request = json.loads((out_dir / "request.json").read_text(encoding="utf-8"))
    assert request["state"] == "submitting"
    assert request["request_id"] is None
    err = capsys.readouterr().err
    assert f"HTTP {code}" in err
    assert "Whether fal.ai queued the request is unknown" in err
    assert fal_run.main(["cost", "--run-dir", str(tmp_path)]) == 0
    assert json.loads(capsys.readouterr().out)["estimated_usd"] == 0.3
    assert fal_run.main(run_args(tmp_path, out_dir=out_dir)) == 1  # same directory refused
    assert "got no answer or only a server error" in capsys.readouterr().err


def test_http_error_on_submit_removes_the_reservation(key_in_env, monkeypatch, tmp_path):
    use_network(monkeypatch, {("POST", SUBMIT_URL): [http_error(SUBMIT_URL, 422, "invalid input")]})

    assert fal_run.main(run_args(tmp_path)) == 1

    assert not (tmp_path / "out" / "request.json").exists()


def test_cost_sums_all_requests_of_the_run(tmp_path, capsys):
    write_spent(tmp_path / "icon-a" / "1", 0.45, num_images=3)
    write_spent(tmp_path / "icon-b" / "1", 0.30, num_images=1, state="failed")

    assert fal_run.main(["cost", "--run-dir", str(tmp_path)]) == 0

    summary = json.loads(capsys.readouterr().out)
    assert summary["requests"] == 2
    assert summary["images"] == 4
    assert summary["estimated_usd"] == 0.75
    assert {entry["state"] for entry in summary["entries"]} == {"completed", "failed"}
    assert all({"dir", "request_id", "num_images", "unit_price_usd", "estimated_usd"} <= set(entry)
               for entry in summary["entries"])


def test_cost_fails_on_a_request_without_cost(tmp_path, capsys):
    write_spent(tmp_path / "icon-a" / "1", 0.45, num_images=3)
    (tmp_path / "icon-b" / "1").mkdir(parents=True)
    (tmp_path / "icon-b" / "1" / "request.json").write_text(json.dumps(SUBMITTED), encoding="utf-8")

    assert fal_run.main(["cost", "--run-dir", str(tmp_path)]) == 1

    assert "holds no readable cost" in capsys.readouterr().err


def test_cost_of_a_missing_run_directory_fails(tmp_path, capsys):
    assert fal_run.main(["cost", "--run-dir", str(tmp_path / "missing")]) == 1
    assert "is no directory" in capsys.readouterr().err


# --- errors ---------------------------------------------------------------------------------------------------------


@pytest.mark.parametrize(
    ("detail", "cause"),
    [
        ("User is locked. Reason: Exhausted balance. Top up your balance at fal.ai/dashboard/billing.",
         "balance is exhausted"),
        ("User is locked. Reason: Admin lock. Please contact support@fal.ai", "locked by fal.ai"),
    ],
)
def test_locked_account_stops_with_exit_code_3_without_retry(detail, cause, key_in_env, monkeypatch, tmp_path, capsys):
    error = http_error(SUBMIT_URL, 403, json.dumps({"detail": detail}))
    network = use_network(monkeypatch, {("POST", SUBMIT_URL): [error]})
    out_dir = tmp_path / "out"

    assert fal_run.main(run_args(tmp_path, out_dir=out_dir)) == 3

    assert len(network.calls) == 1
    assert not (out_dir / "request.json").exists()
    _, err = assert_secret_absent(capsys)
    assert cause in err
    assert "Do not retry, do not switch to another model, tell Marcus" in err


def test_other_forbidden_answer_is_a_plain_error(key_in_env, monkeypatch, tmp_path, capsys):
    error = http_error(SUBMIT_URL, 403, json.dumps({"detail": "Forbidden"}))
    use_network(monkeypatch, {("POST", SUBMIT_URL): [error]})

    assert fal_run.main(run_args(tmp_path)) == 1
    assert "HTTP 403" in capsys.readouterr().err


def test_rate_limit_waits_for_retry_after_then_retries(key_in_env, monkeypatch, isolated, capsys):
    limited = http_error(PRICING_URL, 429, "slow down", {"Retry-After": "7"})
    network = use_network(monkeypatch, {("GET", PRICING_URL): [limited, {"prices": [{"unit_price": 0.15}]}]})

    assert fal_run.main(["price", ENDPOINT]) == 0

    assert len(network.calls) == 2
    assert isolated == [7.0]
    assert "HTTP 429" in capsys.readouterr().err


def test_rate_limit_gives_up_after_the_last_retry(key_in_env, monkeypatch, isolated, capsys):
    network = use_network(monkeypatch, {("GET", PRICING_URL): [http_error(PRICING_URL, 429, "slow down")]})

    assert fal_run.main(["price", ENDPOINT]) == 1

    assert len(network.calls) == fal_run.MAX_RATE_LIMIT_RETRIES + 1
    assert isolated == [5.0, 10.0, 20.0, 40.0, 80.0]
    assert "HTTP 429" in capsys.readouterr().err


def test_http_error_reports_status_and_body_but_not_headers(key_in_env, monkeypatch, capsys):
    error = http_error(PRICING_URL, 500, "internal failure in the pricing service",
                       {"X-Request-Header-Value": "header-only-marker"})
    use_network(monkeypatch, {("GET", PRICING_URL): [error]})

    assert fal_run.main(["price", ENDPOINT]) == 1

    err = capsys.readouterr().err
    assert "HTTP 500" in err
    assert "internal failure in the pricing service" in err
    assert "header-only-marker" not in err


def test_key_echoed_in_an_error_body_is_redacted(key_in_env, monkeypatch, capsys):
    use_network(monkeypatch, {("GET", PRICING_URL): [http_error(PRICING_URL, 401, f"invalid key Key {SECRET}")]})

    assert fal_run.main(["price", ENDPOINT]) == 1

    _, err = assert_secret_absent(capsys)
    assert "Key ***" in err


def test_key_across_the_snippet_limit_is_redacted(key_in_env, monkeypatch, capsys):
    # The key starts 7 characters before the cut: cut first, redact second would leave its beginning.
    body = "x" * (fal_run.BODY_SNIPPET_CHARS - 12) + f" Key {SECRET}"
    use_network(monkeypatch, {("GET", PRICING_URL): [http_error(PRICING_URL, 401, body)]})

    assert fal_run.main(["price", ENDPOINT]) == 1

    _, err = assert_secret_absent(capsys)
    assert SECRET[:5] not in err


def test_http_401_points_to_check_without_retry(key_in_env, monkeypatch, capsys):
    network = use_network(monkeypatch, {("GET", PRICING_URL): [http_error(PRICING_URL, 401, "Invalid key")]})

    assert fal_run.main(["price", ENDPOINT]) == 1

    assert len(network.calls) == 1
    _, err = assert_secret_absent(capsys)
    assert "HTTP 401" in err
    assert "fal_run.py check" in err
    assert "Do not retry" in err


def test_timeout_keeps_request_and_resume_finishes_it(key_in_env, monkeypatch, tmp_path, capsys):
    out_dir = tmp_path / "out"
    # A second status request would mean polling past the timeout: fail instead of looping forever.
    polled_too_long = RuntimeError("polled past the timeout")
    network = use_network(monkeypatch, {("POST", SUBMIT_URL): [SUBMITTED],
                                        ("GET", STATUS_URL): [{"status": "IN_PROGRESS"}, polled_too_long]})

    code = fal_run.main(run_args(tmp_path, "--timeout", "0", out_dir=out_dir))

    assert code == 4
    assert "resume --out" in capsys.readouterr().err
    request = json.loads((out_dir / "request.json").read_text(encoding="utf-8"))
    assert request["request_id"] == "req-123"
    assert request["state"] == "queued"
    assert not (out_dir / "result.json").exists()

    network = use_network(monkeypatch, completed_routes())
    assert fal_run.main(["resume", "--out", str(out_dir)]) == 0

    assert network.calls_to(SUBMIT_URL) == []
    assert (out_dir / "req-123-1.png").read_bytes() == PNG_BYTES
    assert json.loads((out_dir / "result.json").read_text(encoding="utf-8"))["request_id"] == "req-123"
    assert_secret_absent(capsys, out_dir / "request.json", out_dir / "result.json")


def test_failed_request_is_not_resumable_and_stays_counted(key_in_env, monkeypatch, tmp_path, capsys):
    routes = {("POST", SUBMIT_URL): [SUBMITTED], ("GET", STATUS_URL): [{"status": "COMPLETED", "error": "bad prompt"}]}
    use_network(monkeypatch, routes)
    out_dir = tmp_path / "out"

    assert fal_run.main(run_args(tmp_path, out_dir=out_dir)) == 1

    err = capsys.readouterr().err
    assert "bad prompt" in err
    assert "resume --out" not in err
    request = json.loads((out_dir / "request.json").read_text(encoding="utf-8"))
    assert request["state"] == "failed"
    assert fal_run.main(["resume", "--out", str(out_dir)]) == 1
    assert "cannot be resumed" in capsys.readouterr().err
    assert fal_run.main(["cost", "--run-dir", str(tmp_path)]) == 0
    assert json.loads(capsys.readouterr().out)["estimated_usd"] == 0.15


def test_response_without_images_is_not_resumable(key_in_env, monkeypatch, tmp_path, capsys):
    use_network(monkeypatch, {("POST", SUBMIT_URL): [SUBMITTED], **completed_routes({"images": []})})

    assert fal_run.main(run_args(tmp_path)) == 1

    err = capsys.readouterr().err
    assert "holds no images" in err
    assert "resume --out" not in err


def test_broken_download_keeps_the_resume_hint(key_in_env, monkeypatch, tmp_path, capsys):
    routes = {("POST", SUBMIT_URL): [SUBMITTED], **completed_routes()}
    routes[("GET", CDN_URL)] = [http.client.IncompleteRead(b"part", 100)]
    use_network(monkeypatch, routes)

    assert fal_run.main(run_args(tmp_path)) == 1

    _, err = assert_secret_absent(capsys)
    assert "IncompleteRead" in err
    assert "resume --out" in err


def test_invalid_data_uri_in_the_response_keeps_the_resume_hint(key_in_env, monkeypatch, tmp_path, capsys):
    broken = {"images": [{"url": "data:image/png;base64,abc"}]}
    use_network(monkeypatch, {("POST", SUBMIT_URL): [SUBMITTED], **completed_routes(broken)})

    assert fal_run.main(run_args(tmp_path)) == 1

    err = capsys.readouterr().err
    assert "no valid base64 data URI" in err
    assert "resume --out" in err


def test_unexpected_error_is_caught_without_a_traceback(key_in_env, monkeypatch, capsys):
    use_network(monkeypatch, {("GET", PRICING_URL): [RuntimeError(f"boom with Key {SECRET}")]})

    assert fal_run.main(["price", ENDPOINT]) == 1

    _, err = assert_secret_absent(capsys)
    assert "unexpected RuntimeError" in err
    assert "Traceback" not in err


# --- helpers --------------------------------------------------------------------------------------------------------


@pytest.mark.parametrize(
    ("url", "expected"),
    [
        ("https://queue.fal.run/fal-ai/x", True),
        ("https://api.fal.ai/v1/models/pricing", True),
        ("https://fal.run/fal-ai/x", True),
        ("https://v3b.fal.media/files/a.png", False),
        ("http://queue.fal.run/fal-ai/x", False),
        ("https://evil-fal.run/x", False),
        ("https://queue.fal.run.example.com/x", False),
    ],
)
def test_key_goes_only_to_fal_hosts_over_https(url, expected):
    assert fal_run.is_fal_host(url) is expected


@pytest.mark.parametrize(
    "target",
    [
        "https://collector.example.com/steal",  # another host
        "http://queue.fal.run/fal-ai/nano-banana-pro/requests/req-123",  # the same host over http
        "https://queue.fal.run/fal-ai/nano-banana-pro/requests/req-456",  # even a fal.ai host over https
    ],
)
@pytest.mark.parametrize(("method", "code"), [("GET", 301), ("GET", 302), ("GET", 307), ("GET", 308), ("POST", 303)])
def test_key_does_not_follow_a_redirect(target, method, code):
    data = b"{}" if method == "POST" else None
    request = fal_run.build_request(STATUS_URL, SECRET, method, data, {"X-Fal-Store-IO": "0"})
    assert headers_of(request)["authorization"] == f"Key {SECRET}"

    # The redirect step of the real urllib handler, the one urlopen runs on a 30x answer.
    redirected = urllib.request.HTTPRedirectHandler().redirect_request(request, None, code, "Found", {}, target)

    assert redirected.full_url == target
    assert "authorization" not in headers_of(redirected)
    assert headers_of(redirected)["x-fal-store-io"] == "0"


def test_sent_request_carries_the_key_only_unredirected(key_in_env, monkeypatch):
    network = use_network(monkeypatch, {("GET", STATUS_URL): [{"status": "IN_QUEUE"}]})

    fal_run.request_json(STATUS_URL, SECRET)

    (sent,) = network.calls
    assert sent.unredirected_hdrs["Authorization"] == f"Key {SECRET}"
    redirected = urllib.request.HTTPRedirectHandler().redirect_request(
        sent, None, 302, "Found", {}, "http://elsewhere.example/status")
    assert "authorization" not in headers_of(redirected)


def test_retry_after_is_capped_and_falls_back_to_backoff():
    message = email.message.Message()
    message["Retry-After"] = "900"
    assert fal_run.retry_after_seconds(message, 0) == fal_run.MAX_RETRY_WAIT_SECONDS
    assert fal_run.retry_after_seconds(email.message.Message(), 2) == 20.0
