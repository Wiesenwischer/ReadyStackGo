"""Small fal.ai client for the vorhaben-grafik skill, standard library only.

Subcommands:
  check                  Report whether FAL_KEY is available and usable. Never prints the key.
  price <endpoint_id>    Print the unit price of an endpoint as JSON.
  run <endpoint_id> --json <input.json> [--image <path> ...] --run-dir <run> --out <dir inside run>
      [--unit-price <usd>] [--budget <usd>] [--spent-before <usd>]
                         Check the budget, record the planned cost, queue a request, wait for it, download the images
                         at once and write result.json.
  resume --out <dir>     Continue a queued request that was not fetched yet, from the request.json it left in <dir>.
  cost --run-dir <run>   Sum the estimated cost of every request of a run from its request.json files. No key needed.

Exit codes: 0 success, 1 error (a missing or unusable key included), 2 usage error, 3 fal.ai account locked (balance
exhausted or locked by fal.ai: do not retry, do not switch models, tell Marcus), 4 request still running after the
timeout (continue with "resume"), 5 budget exceeded (nothing was sent: ask Marcus).

The key comes from HKCU\\Environment on Windows, which wins because a key set or replaced there after the app started is
used without a restart, and otherwise from the process environment. It is sent only to fal.ai hosts over https, never
follows a redirect and is never printed, logged or written to a file.
"""

from __future__ import annotations

import argparse
import base64
import binascii
import email.utils
import http.client
import json
import math
import os
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from datetime import datetime, timezone
from pathlib import Path

try:
    import winreg
except ImportError:  # not Windows
    winreg = None

KEY_NAME = "FAL_KEY"
KEYS_PAGE = "https://fal.ai/dashboard/keys"
QUEUE_BASE = "https://queue.fal.run"
PRICING_URL = "https://api.fal.ai/v1/models/pricing"
FAL_DOMAINS = ("fal.run", "fal.ai")

# Generated media stay on the fal CDN only for a day; the images are downloaded right after the request anyway.
MEDIA_LIFETIME_SECONDS = 86400
HTTP_TIMEOUT_SECONDS = 60
MAX_RATE_LIMIT_RETRIES = 5
MAX_RETRY_WAIT_SECONDS = 120
BODY_SNIPPET_CHARS = 500

# Proposal of 27.09.2026 until Marcus names another limit.
DEFAULT_BUDGET_USD = 3.0
# List price per image, model page.
KNOWN_UNIT_PRICES_USD = {"fal-ai/nano-banana-pro/edit": {"1K": 0.15, "2K": 0.15, "4K": 0.30}}

REQUEST_FILE = "request.json"
RESULT_FILE = "result.json"

# States of a request in its request.json.
STATE_SUBMITTING = "submitting"  # reserved before the submit; stays if the submit got no answer
STATE_QUEUED = "queued"
STATE_COMPLETED = "completed"
STATE_FAILED = "failed"  # fal.ai reported the request as failed; it stays counted

EXIT_OK = 0
EXIT_ERROR = 1
EXIT_ACCOUNT_LOCKED = 3
EXIT_PENDING = 4
EXIT_BUDGET = 5

IMAGE_TYPES = {".png": "image/png", ".jpg": "image/jpeg", ".jpeg": "image/jpeg", ".webp": "image/webp"}
EXTENSIONS = {"image/png": ".png", "image/jpeg": ".jpg", "image/webp": ".webp"}

# Indirections the tests replace, so that no test waits.
_sleep = time.sleep
_monotonic = time.monotonic


class FalError(Exception):
    """An error with a message for the user and the exit code to end with.

    resumable is False when fal.ai itself reported the request as failed, so that fetching it again cannot help.
    http_status is set when fal.ai answered with an HTTP error.
    """

    def __init__(self, message: str, exit_code: int = EXIT_ERROR, *, resumable: bool = True,
                 http_status: int | None = None):
        super().__init__(message)
        self.exit_code = exit_code
        self.resumable = resumable
        self.http_status = http_status


def log(message: str) -> None:
    print(message, file=sys.stderr, flush=True)


def now_iso() -> str:
    return datetime.now(timezone.utc).replace(microsecond=0).isoformat()


# --- Key ------------------------------------------------------------------------------------------------------------


def find_key() -> tuple[str | None, str | None]:
    """Return (key, where it was found), or (None, None). Never raises: main needs it for redacting.

    On Windows the user variable in HKCU\\Environment wins: the process environment is a copy taken when the app
    started and would keep a replaced or revoked key until the app restarts.
    """
    value = read_user_environment(KEY_NAME)
    if value:
        return value, "HKCU\\Environment"
    value = os.environ.get(KEY_NAME, "").strip()
    if value:
        return value, "the process environment"
    return None, None


def read_user_environment(name: str) -> str | None:
    """Read a Windows user environment variable straight from the registry."""
    if winreg is None:
        return None
    try:
        with winreg.OpenKey(winreg.HKEY_CURRENT_USER, "Environment") as handle:
            value, value_type = winreg.QueryValueEx(handle, name)
    except OSError:
        return None
    if not isinstance(value, str):
        return None
    if value_type == getattr(winreg, "REG_EXPAND_SZ", object()):
        value = os.path.expandvars(value)
    return value.strip() or None


def missing_key_message() -> str:
    return (
        f"{KEY_NAME} was found neither in the process environment nor in HKCU\\Environment. "
        f"Create a key at {KEYS_PAGE}, set it as the Windows user environment variable {KEY_NAME} and restart the "
        "app. Never paste the key into a chat, a file or a command line."
    )


def is_usable_key(key: str) -> bool:
    """A key goes into an HTTP header: visible ASCII only, no spaces, tabs or line breaks."""
    return all("!" <= char <= "~" for char in key)


def unusable_key_message(source: str | None) -> str:
    # Names only where the key is, never the key or a part of it.
    return (
        f"{KEY_NAME} in {source} contains spaces, tabs, line breaks or characters outside visible ASCII and cannot be "
        f"sent. Copy the key again from {KEYS_PAGE} as a single line, set {KEY_NAME} anew and restart the app. "
        "Never paste the key into a chat, a file or a command line."
    )


def require_key() -> str:
    key, source = find_key()
    if not key:
        raise FalError(missing_key_message())
    if not is_usable_key(key):
        raise FalError(unusable_key_message(source))
    return key


def redact(text: str, key: str | None) -> str:
    """Replace the key wherever it shows up in text meant for output."""
    if key and text:
        return text.replace(key, "***")
    return text


# --- HTTP -----------------------------------------------------------------------------------------------------------


def is_fal_host(url: str) -> bool:
    """True for https URLs on fal.ai's own hosts, the only ones that get the key."""
    parts = urllib.parse.urlsplit(url)
    host = (parts.hostname or "").lower()
    return parts.scheme == "https" and any(host == domain or host.endswith("." + domain) for domain in FAL_DOMAINS)


def describe(url: str) -> str:
    """The URL without query and fragment, for messages."""
    parts = urllib.parse.urlsplit(url)
    return urllib.parse.urlunsplit((parts.scheme, parts.netloc, parts.path, "", ""))


def retry_after_seconds(headers, attempt: int) -> float:
    """Seconds to wait after HTTP 429: Retry-After (seconds or HTTP date), else an exponential backoff."""
    value = headers.get("Retry-After") if headers is not None else None
    wait = None
    if value:
        value = value.strip()
        try:
            wait = float(value)
        except ValueError:
            try:
                wait = (email.utils.parsedate_to_datetime(value) - datetime.now(timezone.utc)).total_seconds()
            except (TypeError, ValueError):
                wait = None
    if wait is None:
        wait = 5.0 * 2 ** attempt
    return min(max(wait, 1.0), MAX_RETRY_WAIT_SECONDS)


def snippet(text: str, key: str | None) -> str:
    """A flattened, shortened body for messages. Redacted before it is cut, so that no cut can split the key."""
    flat = " ".join(text.split())
    return redact(flat, key)[:BODY_SNIPPET_CHARS]


def http_error(status: int, url: str, body: str, key: str | None) -> FalError:
    """Turn an HTTP error into a FalError with status and a body snippet. Headers are never included."""
    text = snippet(body, key)
    lowered = body.lower()
    if status == 403 and ("locked" in lowered or "balance" in lowered or "exhausted" in lowered):
        if "balance" in lowered or "exhausted" in lowered:
            cause = "the account balance is exhausted and has to be topped up at https://fal.ai/dashboard/billing"
        else:
            cause = (
                "the account is locked by fal.ai; only the account owner can have it lifted (support@fal.ai). "
                "A lock that stays after a top-up is a known fal.ai fault (fal-ai/fal#922, #1168)"
            )
        return FalError(
            f"HTTP 403 from fal.ai: {cause} ({text}). Do not retry, do not switch to another model, tell Marcus.",
            EXIT_ACCOUNT_LOCKED,
            http_status=status,
        )
    if status == 401:
        return FalError(
            f"HTTP 401 from {describe(url)}: fal.ai rejected {KEY_NAME} ({text or '(empty body)'}). "
            f"Run 'fal_run.py check' to see which {KEY_NAME} is used. If the key was revoked or replaced, create a new "
            f"one at {KEYS_PAGE} and set it as the Windows user environment variable {KEY_NAME}. Do not retry; tell "
            "Marcus.",
            http_status=status,
        )
    return FalError(f"HTTP {status} from {describe(url)}: {text or '(empty body)'}", http_status=status)


def read_error_body(error: urllib.error.HTTPError) -> str:
    try:
        return error.read().decode("utf-8", "replace")
    except Exception:  # the body is only used for the message
        return ""


def build_request(url: str, key: str | None, method: str, data: bytes | None,
                  headers: dict[str, str]) -> urllib.request.Request:
    """A request that carries the key only for https fal.ai hosts and only to the URL it was built for.

    The key is an unredirected header: urllib copies ordinary headers to every redirect target, whatever its host or
    scheme, but never unredirected ones. A redirect therefore arrives without the key, even at a fal.ai host.
    """
    request = urllib.request.Request(url, data=data, headers=headers, method=method)
    if key and is_fal_host(url):
        request.add_unredirected_header("Authorization", f"Key {key}")
    return request


def http_request(url: str, key: str | None = None, method: str = "GET", payload=None, headers=None) -> bytes:
    """Send a request and return the response body.

    The key is attached only for https fal.ai hosts and never follows a redirect. HTTP 429 is retried after
    Retry-After; every other HTTP error raises a FalError with the status and a snippet of the body.
    """
    data = None
    all_headers = {"User-Agent": "vorhaben-grafik-fal-run/1"}
    if payload is not None:
        data = json.dumps(payload).encode("utf-8")
        all_headers["Content-Type"] = "application/json"
    all_headers.update(headers or {})
    attempt = 0
    while True:
        try:
            request = build_request(url, key, method, data, all_headers)
            with urllib.request.urlopen(request, timeout=HTTP_TIMEOUT_SECONDS) as response:
                return response.read()
        except urllib.error.HTTPError as error:
            body = read_error_body(error)
            if error.code == 429 and attempt < MAX_RATE_LIMIT_RETRIES:
                wait = retry_after_seconds(error.headers, attempt)
                log(f"HTTP 429 from {describe(url)}: rate limited, retrying in {wait:.0f} s.")
                _sleep(wait)
                attempt += 1
                continue
            raise http_error(error.code, url, body, key) from None
        except OSError as error:  # URLError, timeouts, refused connections
            reason = getattr(error, "reason", error)
            raise FalError(redact(f"Request to {describe(url)} failed: {reason}", key)) from None
        except http.client.HTTPException as error:  # IncompleteRead, BadStatusLine: no OSError
            raise FalError(redact(f"Request to {describe(url)} failed: {type(error).__name__}: {error}", key)) from None
        except ValueError as error:  # http.client quotes a rejected header value in its message, so leave it out
            raise FalError(f"Request to {describe(url)} could not be sent ({type(error).__name__}).") from None


def request_json(url: str, key: str | None = None, method: str = "GET", payload=None, headers=None):
    raw = http_request(url, key, method, payload, {"Accept": "application/json", **(headers or {})})
    try:
        return json.loads(raw.decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError):
        text = snippet(raw.decode("utf-8", "replace"), key)
        raise FalError(f"Response from {describe(url)} is not JSON: {text}") from None


# --- Files ----------------------------------------------------------------------------------------------------------


def write_json(path: Path, data) -> None:
    path.write_text(json.dumps(data, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def read_json_record(path: Path) -> dict:
    """The JSON object in path, or {} when it cannot be read."""
    try:
        record = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return {}
    return record if isinstance(record, dict) else {}


def load_input(path: Path) -> dict:
    try:
        payload = json.loads(path.read_text(encoding="utf-8-sig"))
    except OSError as error:
        raise FalError(f"Cannot read {path}: {error.strerror or error}") from None
    except json.JSONDecodeError as error:
        raise FalError(f"{path} is not valid JSON: {error}") from None
    if not isinstance(payload, dict):
        raise FalError(f"{path} must hold a JSON object.")
    return payload


def attach_images(payload: dict, paths: list[Path]) -> dict[str, str]:
    """Append each image as a base64 data URI to image_urls. Returns {data URI: path} for the record."""
    if not paths:
        return {}
    urls = payload.setdefault("image_urls", [])
    if not isinstance(urls, list):
        raise FalError("image_urls in the input must be a list.")
    attached = {}
    for path in paths:
        mime = IMAGE_TYPES.get(path.suffix.lower())
        if not mime:
            raise FalError(f"{path}: only PNG, JPEG and WebP images can be sent.")
        try:
            data = path.read_bytes()
        except OSError as error:
            raise FalError(f"Cannot read {path}: {error.strerror or error}") from None
        uri = f"data:{mime};base64,{base64.b64encode(data).decode('ascii')}"
        urls.append(uri)
        attached[uri] = str(path)
    return attached


def describe_input(payload: dict, attached: dict[str, str]) -> dict:
    """The request input for the record, with data URIs replaced by the file they came from."""
    record = dict(payload)
    if isinstance(payload.get("image_urls"), list):
        entries = []
        for url in payload["image_urls"]:
            if url in attached:
                entries.append(f"file:{attached[url]}")
            elif isinstance(url, str) and url.startswith("data:"):
                entries.append(f"data URI ({len(url)} characters)")
            else:
                entries.append(url)
        record["image_urls"] = entries
    return record


def ensure_fresh_output_dir(out_dir: Path) -> None:
    """Refuse a directory that already holds a request, so that no paid request is submitted twice or overwritten."""
    if (out_dir / RESULT_FILE).exists():
        raise FalError(f"{out_dir / RESULT_FILE} already exists: use a fresh --out directory for every request.")
    request_path = out_dir / REQUEST_FILE
    if request_path.exists():
        record = read_json_record(request_path)
        request_id = record.get("request_id")
        state = record.get("state")
        if state == STATE_FAILED:
            raise FalError(
                f"Request {request_id} in {request_path} failed at fal.ai and stays counted in the run's cost. "
                "A new request, with a changed input, needs a fresh --out directory."
            )
        if state == STATE_SUBMITTING and not request_id:
            raise FalError(
                f"The submit recorded in {request_path} got no answer or only a server error, so whether fal.ai queued "
                "it is unknown; it stays counted in the run's cost and cannot be resumed. A new request needs a fresh "
                "--out directory."
            )
        raise FalError(
            f"Request {request_id or '(id unreadable)'} in {request_path} was already submitted and is paid for, "
            f"but has no {RESULT_FILE}. Do not submit it again; fetch it with: fal_run.py resume --out {out_dir}"
        )
    out_dir.mkdir(parents=True, exist_ok=True)


def safe_name(text: str) -> str:
    return "".join(char if char.isalnum() or char in "-_" else "_" for char in text) or "request"


def extension_for(content_type: str | None, url: str) -> str:
    suffix = Path(urllib.parse.urlsplit(url).path).suffix.lower() if not url.startswith("data:") else ""
    if suffix in IMAGE_TYPES:
        return ".jpg" if suffix == ".jpeg" else suffix
    return EXTENSIONS.get((content_type or "").split(";")[0].strip().lower(), ".bin")


# --- Cost -----------------------------------------------------------------------------------------------------------


def require_amount(value: float, name: str) -> float:
    """A USD amount from the command line: finite and not negative, so that every budget comparison holds."""
    if not math.isfinite(value) or value < 0:
        raise FalError(f"{name} must be a finite amount of at least 0 USD, not {value}. Nothing was sent.")
    return value


def planned_cost(endpoint: str, payload: dict, unit_price: float | None) -> dict:
    """The estimated cost of a request, known before anything is sent."""
    if payload.get("enable_web_search"):
        raise FalError("enable_web_search adds a charge per request that the cost estimate does not count; leave it "
                       "out of the input. Nothing was sent.")
    count = payload.get("num_images")
    if isinstance(count, bool) or not isinstance(count, int) or count < 1:
        raise FalError("The input needs num_images as a whole number of at least 1, so that the cost is known before "
                       "the request.")
    resolution = payload.get("resolution")
    prices = KNOWN_UNIT_PRICES_USD.get(endpoint, {})
    known = prices.get(resolution) if isinstance(resolution, str) else None
    if unit_price is not None:
        require_amount(unit_price, "--unit-price")
        # 'fal_run.py price' reports the base price only; an option must never undercut a known list price. Without a
        # known resolution the highest list price of the endpoint is the floor.
        floor = known if known is not None else max(prices.values(), default=None)
        if floor is not None and unit_price < floor:
            raise FalError(
                f"--unit-price {unit_price} is below the list price of {endpoint} at resolution {resolution!r} "
                f"({floor} USD per image). Leave --unit-price out for this endpoint and set resolution in the input. "
                "Nothing was sent."
            )
        price, source = unit_price, "--unit-price"
    else:
        if known is None:
            raise FalError(
                f"No known price for {endpoint} at resolution {resolution!r}: set resolution in the input or pass "
                f"--unit-price from 'fal_run.py price {endpoint}'."
            )
        price, source = known, "known price list"
    return {
        "num_images": count,
        "unit_price_usd": price,
        "estimated_usd": round(count * price, 4),
        "price_source": source,
    }


def ledger(run_dir: Path) -> list[dict]:
    """Every request of a run with its estimated cost.

    A request.json without a readable cost fails the whole count, so that the run is never undercounted.
    """
    entries = []
    for path in sorted(run_dir.rglob(REQUEST_FILE)):
        try:
            record = json.loads(path.read_text(encoding="utf-8"))
            cost = record["cost"]
            estimated = float(cost["estimated_usd"])
            if not math.isfinite(estimated) or estimated < 0:
                raise ValueError("cost is not a finite amount of at least 0")
        except (OSError, ValueError, KeyError, TypeError) as error:
            raise FalError(
                f"{path} holds no readable cost ({type(error).__name__}), so the cost of the run in {run_dir} cannot "
                "be counted. Nothing was sent."
            ) from None
        entries.append(
            {
                "dir": str(path.parent),
                "request_id": record.get("request_id"),
                "state": record.get("state"),
                "num_images": cost.get("num_images"),
                "unit_price_usd": cost.get("unit_price_usd"),
                "estimated_usd": estimated,
            }
        )
    return entries


def usd(value: float) -> str:
    return f"{value:.2f}"


# --- Queue ----------------------------------------------------------------------------------------------------------


def submit_headers() -> dict[str, str]:
    return {
        # Do not keep the JSON input and output in the fal request history.
        "X-Fal-Store-IO": "0",
        "X-Fal-Object-Lifecycle-Preference": json.dumps({"expiration_duration_seconds": MEDIA_LIFETIME_SECONDS}),
    }


def wait_until_completed(request: dict, key: str, out_dir: Path, timeout: float, poll_interval: float) -> None:
    deadline = _monotonic() + timeout
    last_state = None
    while True:
        status = request_json(request["status_url"], key)
        state = status.get("status") if isinstance(status, dict) else None
        if state != last_state:
            log(f"Request {request['request_id']}: {state}")
            last_state = state
        if state == "COMPLETED":
            if status.get("error"):
                raise FalError(
                    f"Request {request['request_id']} failed: {snippet(str(status['error']), key)}", resumable=False
                )
            return
        if _monotonic() >= deadline:
            raise FalError(
                f"Request {request['request_id']} is still {state} after {timeout:.0f} s. Do not submit it again; "
                f"continue with: fal_run.py resume --out {out_dir}",
                EXIT_PENDING,
            )
        _sleep(poll_interval)


def download_images(response: dict, request_id: str, out_dir: Path, key: str) -> list[dict]:
    images = response.get("images") if isinstance(response, dict) else None
    if not isinstance(images, list) or not images:
        raise FalError(f"The response holds no images: {snippet(json.dumps(response), key)}", resumable=False)
    outputs = []
    for index, image in enumerate(images, start=1):
        url = image.get("url") if isinstance(image, dict) else None
        if not url:
            raise FalError(f"Image {index} of request {request_id} has no URL.")
        content_type = image.get("content_type")
        if url.startswith("data:"):
            header, _, encoded = url.partition(",")
            try:
                data = base64.b64decode(encoded)
            except binascii.Error:
                raise FalError(f"Image {index} of request {request_id} is no valid base64 data URI.") from None
            content_type = content_type or header[len("data:"):].split(";")[0]
            source = "data URI (inline in the response)"
        else:
            data = http_request(url, key)  # the CDN is no fal.ai API host, it never gets the key
            source = url
        if not data:
            raise FalError(f"Image {index} of request {request_id} is empty.")
        name = f"{safe_name(request_id)}-{index}{extension_for(content_type, url)}"
        (out_dir / name).write_bytes(data)
        outputs.append(
            {
                "file": name,
                "url": source,
                "content_type": content_type,
                "width": image.get("width"),
                "height": image.get("height"),
            }
        )
    return outputs


def finish(request: dict, key: str, out_dir: Path, timeout: float, poll_interval: float) -> int:
    try:
        wait_until_completed(request, key, out_dir, timeout, poll_interval)
        response = request_json(request["response_url"], key)
        outputs = download_images(response, request["request_id"], out_dir, key)
    except FalError as error:
        if error.exit_code != EXIT_ERROR:
            raise
        if not error.resumable:
            request["state"] = STATE_FAILED
            write_json(out_dir / REQUEST_FILE, request)
            raise FalError(
                f"{error} fal.ai did not deliver this request, so it cannot be resumed; it stays counted in the "
                "run's cost. A new request, with a changed input, needs a fresh --out directory.",
                resumable=False,
            ) from None
        raise FalError(
            f"{error} The request is recorded in {out_dir / REQUEST_FILE}; "
            f"to fetch it again: fal_run.py resume --out {out_dir}"
        ) from None
    request_input = request.get("input") or {}
    result = {
        "request_id": request["request_id"],
        "endpoint": request["endpoint"],
        "submitted_at": request.get("submitted_at"),
        "completed_at": now_iso(),
        "seed": response.get("seed", request_input.get("seed")),
        "cost": request.get("cost"),
        "input": request_input,
        "outputs": outputs,
    }
    if response.get("description"):
        result["description"] = response["description"]
    request["state"] = STATE_COMPLETED
    write_json(out_dir / REQUEST_FILE, request)
    write_json(out_dir / RESULT_FILE, result)
    for output in outputs:
        print(out_dir / output["file"])
    print(out_dir / RESULT_FILE)
    return EXIT_OK


# --- Commands -------------------------------------------------------------------------------------------------------


def command_check(_args) -> int:
    key, source = find_key()
    if not key:
        log(missing_key_message())
        return EXIT_ERROR
    if not is_usable_key(key):
        log(unusable_key_message(source))
        return EXIT_ERROR
    if source == "HKCU\\Environment":
        # The values are only compared, never printed.
        in_process = os.environ.get(KEY_NAME, "").strip()
        note = (" This process still holds a different, older value from when the app started; fal_run.py ignores it."
                if in_process and in_process != key else "")
        print(f"{KEY_NAME} found in HKCU\\Environment.{note}")
    else:
        print(f"{KEY_NAME} found in the process environment.")
    return EXIT_OK


def command_price(args) -> int:
    key = require_key()
    query = urllib.parse.urlencode({"endpoint_id": args.endpoint_id}, safe="/")
    result = request_json(f"{PRICING_URL}?{query}", key)
    prices = result.get("prices") if isinstance(result, dict) else None
    if prices == []:
        raise FalError(f"No price listed for {args.endpoint_id}.")
    print(redact(json.dumps(prices if prices is not None else result, indent=2), key))
    return EXIT_OK


def check_budget(run_dir: Path, cost: dict, budget: float, spent_before: float) -> None:
    """Refuse a request that would lift the spending of the grafik PR above the budget. Nothing is sent then."""
    require_amount(budget, "--budget")
    require_amount(spent_before, "--spent-before")
    spent_in_run = sum(entry["estimated_usd"] for entry in ledger(run_dir))
    spent = spent_before + spent_in_run
    if round(spent + cost["estimated_usd"], 4) > round(budget, 4):
        before = f" and {usd(spent_before)} USD in earlier runs on the same grafik PR" if spent_before else ""
        raise FalError(
            f"This run has spent {usd(spent_in_run)} USD{before}, the request would add {usd(cost['estimated_usd'])} "
            f"USD, the limit is {usd(budget)} USD. Nothing was sent. Ask Marcus; only with his yes run again with "
            "--budget <his limit>.",
            EXIT_BUDGET,
        )


def command_run(args) -> int:
    run_dir = Path(args.run_dir)
    out_dir = Path(args.out)
    if not out_dir.resolve().is_relative_to(run_dir.resolve()):
        raise FalError(f"--out {out_dir} must lie inside --run-dir {run_dir}, so that the run counts this request.")
    endpoint = args.endpoint_id.strip("/")
    payload = load_input(Path(args.json_path))
    cost = planned_cost(endpoint, payload, args.unit_price)
    ensure_fresh_output_dir(out_dir)
    check_budget(run_dir, cost, args.budget, args.spent_before)
    key = require_key()
    attached = attach_images(payload, [Path(path) for path in args.image])

    # Reserve the cost before the submit, so that a run that breaks off afterwards still counts it.
    request = {
        "request_id": None,
        "endpoint": endpoint,
        "state": STATE_SUBMITTING,
        "status_url": None,
        "response_url": None,
        "submitted_at": now_iso(),
        "cost": cost,
        "input": describe_input(payload, attached),
    }
    write_json(out_dir / REQUEST_FILE, request)
    try:
        submitted = request_json(f"{QUEUE_BASE}/{endpoint}", key, "POST", payload, submit_headers())
    except FalError as error:
        if error.http_status is not None and 400 <= error.http_status < 500:
            # fal.ai rejected the request itself (4xx), so nothing is queued and nothing is charged.
            (out_dir / REQUEST_FILE).unlink(missing_ok=True)
            raise
        # No answer or a server error (5xx, often from a gateway): the request may be queued and paid for.
        raise FalError(
            f"{error} Whether fal.ai queued the request is unknown; it stays counted in {out_dir / REQUEST_FILE}. "
            "It cannot be resumed; submit again only in a fresh --out directory.",
            error.exit_code,
        ) from None
    if isinstance(submitted, dict):
        request["request_id"] = submitted.get("request_id")
        request["status_url"] = submitted.get("status_url")
        request["response_url"] = submitted.get("response_url")
    if not (request["request_id"] and request["status_url"] and request["response_url"]):
        raise FalError(
            f"The queue answer lacks request_id, status_url or response_url: {snippet(json.dumps(submitted), key)}. "
            f"The request stays counted in {out_dir / REQUEST_FILE}."
        )
    request["state"] = STATE_QUEUED
    write_json(out_dir / REQUEST_FILE, request)
    log(f"Queued request {request['request_id']} for {endpoint} ({usd(cost['estimated_usd'])} USD estimated).")
    return finish(request, key, out_dir, args.timeout, args.poll_interval)


def command_resume(args) -> int:
    out_dir = Path(args.out)
    request_path = out_dir / REQUEST_FILE
    if (out_dir / RESULT_FILE).exists():
        raise FalError(f"{out_dir / RESULT_FILE} already exists: this request is finished.")
    try:
        request = json.loads(request_path.read_text(encoding="utf-8"))
    except (OSError, ValueError) as error:
        raise FalError(f"Cannot read {request_path}: {error}") from None
    if not isinstance(request, dict):
        raise FalError(f"{request_path} must hold a JSON object.")
    if request.get("state") == STATE_FAILED:
        raise FalError(
            f"Request {request.get('request_id')} in {request_path} failed at fal.ai and cannot be resumed. "
            "A new request, with a changed input, needs a fresh --out directory."
        )
    if not (request.get("request_id") and request.get("status_url") and request.get("response_url")):
        raise FalError(
            f"{request_path} holds no queued request (the submit got no answer or only a server error), so there is "
            "nothing to resume. "
            "It stays counted in the run's cost; a new request needs a fresh --out directory."
        )
    key = require_key()
    return finish(request, key, out_dir, args.timeout, args.poll_interval)


def command_cost(args) -> int:
    run_dir = Path(args.run_dir)
    if not run_dir.is_dir():
        raise FalError(f"{run_dir} is no directory: name the run directory that the requests were run with.")
    entries = ledger(run_dir)
    summary = {
        "run_dir": str(run_dir),
        "requests": len(entries),
        "images": sum(entry["num_images"] for entry in entries if isinstance(entry["num_images"], int)),
        "estimated_usd": round(sum(entry["estimated_usd"] for entry in entries), 4),
        "entries": entries,
    }
    print(json.dumps(summary, indent=2, ensure_ascii=False))
    return EXIT_OK


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(prog="fal_run.py", description="fal.ai helper of the vorhaben-grafik skill.")
    commands = parser.add_subparsers(dest="command", required=True)

    commands.add_parser("check", help="report whether FAL_KEY is available and usable, never its value")

    price = commands.add_parser("price", help="print the unit price of an endpoint")
    price.add_argument("endpoint_id")

    def add_wait_options(command):
        command.add_argument("--timeout", type=float, default=100.0,
                             help="seconds to wait for the result; stay below the calling tool's timeout")
        command.add_argument("--poll-interval", type=float, default=2.0, help="seconds between status requests")

    run = commands.add_parser("run", help="check the budget, queue a request, wait, download the images")
    run.add_argument("endpoint_id")
    run.add_argument("--json", dest="json_path", required=True,
                     help="request input as a JSON object, with num_images and, for known prices, resolution")
    run.add_argument("--image", action="append", default=[], help="image to append to image_urls as a data URI")
    run.add_argument("--run-dir", required=True,
                     help="directory of this run; every request.json below it counts toward the budget")
    run.add_argument("--out", required=True,
                     help="fresh directory inside --run-dir for images, request.json and result.json")
    run.add_argument("--unit-price", type=float, default=None,
                     help="price per image in USD from 'fal_run.py price', for endpoints without a known price")
    run.add_argument("--budget", type=float, default=DEFAULT_BUDGET_USD,
                     help="limit in USD for the grafik PR; raise it only with Marcus' yes")
    run.add_argument("--spent-before", type=float, default=0.0,
                     help="USD already spent on the same grafik PR in earlier runs (its \"Kosten gesamt\")")
    add_wait_options(run)

    resume = commands.add_parser("resume", help="continue a queued request that was not fetched yet")
    resume.add_argument("--out", required=True, help="directory of the earlier request")
    add_wait_options(resume)

    cost = commands.add_parser("cost", help="sum the estimated cost of every request of a run")
    cost.add_argument("--run-dir", required=True, help="directory of the run")
    return parser


def main(argv: list[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    handlers = {
        "check": command_check,
        "price": command_price,
        "run": command_run,
        "resume": command_resume,
        "cost": command_cost,
    }
    try:
        return handlers[args.command](args)
    except FalError as error:
        log(f"Error: {redact(str(error), find_key()[0])}")
        return error.exit_code
    except KeyboardInterrupt:
        log("Interrupted.")
        return 130
    except Exception as error:  # last net: a traceback could quote a header value
        log(f"Error: unexpected {type(error).__name__}: {redact(str(error), find_key()[0])}")
        return EXIT_ERROR


if __name__ == "__main__":
    sys.exit(main())
