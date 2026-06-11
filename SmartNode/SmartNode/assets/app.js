        const API = 'http://localhost:8080';
        const chatMessages = document.getElementById('chat-messages');
        const chatInput = document.getElementById('chat-input');
        const sendBtn = document.getElementById('send-btn');
        const typingIndicator = document.getElementById('typing-indicator');
        const statusDot = document.getElementById('status-dot');
        const statusText = document.getElementById('status-text');
        const smartnodeStatus = document.getElementById('smartnode-status');
        const haStatus = document.getElementById('ha-status');
        const entityCount = document.getElementById('entity-count');
        const liveDataBadge = document.getElementById('live-data-badge');
        const nordpoolBadge = document.getElementById('nordpool-badge');
        const energySourceStatus = document.getElementById('energy-source-status');
        const energySourceHint = document.getElementById('energy-source-hint');
        const dashEnergy = document.getElementById('dash-energy');
        const dashEnergyHint = document.getElementById('dash-energy-hint');
        const modelHealthBadge = document.getElementById('model-health-badge');
        const modelHealthSummary = document.getElementById('model-health-summary');
        const modelHealthProfile = document.getElementById('model-health-profile');
        const modelHealthSource = document.getElementById('model-health-source');
        const modelHealthSensors = document.getElementById('model-health-sensors');
        const modelHealthActuators = document.getElementById('model-health-actuators');
        const modelHealthErrors = document.getElementById('model-health-errors');
        const modelHealthWarnings = document.getElementById('model-health-warnings');
        const modelHealthMode = document.getElementById('model-health-mode');
        const modelHealthLive = document.getElementById('model-health-live');
        const modelHealthEntities = document.getElementById('model-health-entities');
        const modelHealthServices = document.getElementById('model-health-services');
        const modelHealthIssues = document.getElementById('model-health-issues');
        const modelHealthRefresh = document.getElementById('model-health-refresh');
        const modelHealthLiveRefresh = document.getElementById('model-health-live-refresh');

        let energyPrices = [];
        let priceSnapshot = null;
        let modelHealthSnapshot = null;
        let haStates = {};
        let entityCatalog = []; // built from haStates; primary source of truth for entity resolution
        let haServices = {};    // dynamic per-domain service registry from /api/ha/services
        let haConfig = null;    // /api/ha/config (location_name, version, ...)

        // Domains we allow to be ON/OFF/triggered from the chatbox.
        // Sensors, weather, zones, etc. stay out — they are read-only (but still queryable).
        const ACTIONABLE_DOMAINS = new Set([
            'light', 'switch', 'input_boolean', 'scene', 'script',
            'automation', 'fan', 'climate', 'cover', 'media_player',
            'lock', 'alarm_control_panel', 'input_number', 'number',
            'select', 'input_select', 'humidifier', 'water_heater',
            'vacuum', 'remote', 'siren', 'valve', 'button', 'input_button'
        ]);

        // Read-only domains that can be queried but not actuated.
        const QUERYABLE_ONLY_DOMAINS = new Set([
            'sensor', 'binary_sensor', 'weather', 'sun', 'zone',
            'person', 'device_tracker', 'calendar', 'image', 'camera'
        ]);

        // Domain priority tiers — drives scoring bonuses and command-kind filtering.
        //   Tier 1 = first-class controllable devices (THE thing the user usually means)
        //   Tier 2 = pre-arranged macros / modes
        //   Tier 3 = side-effect rules — included ONLY when the user explicitly asks for one
        const DOMAIN_TIERS = {
            'light': 1, 'switch': 1, 'input_boolean': 1,
            'fan': 1, 'cover': 1, 'media_player': 1, 'climate': 1,
            'lock': 1, 'humidifier': 1, 'water_heater': 1, 'vacuum': 1,
            'input_number': 1, 'number': 1, 'select': 1, 'input_select': 1,
            'valve': 1, 'siren': 1, 'remote': 1, 'button': 1, 'input_button': 1,
            'alarm_control_panel': 1,
            'scene': 2, 'script': 2,
            'automation': 3
        };

        // Per-domain capabilities — what services are valid, what action verb maps to what service.
        // This drives the resolver: "turn on" + domain=cover → cover.open_cover, "set 21" + domain=climate
        // → climate.set_temperature with {temperature: 21}. Confirmation-required marks unsafe ops.
        const QUICK_ACTION_DOMAIN_PRIORITY = {
            light: 1,
            switch: 2,
            cover: 3,
            climate: 4,
            fan: 5,
            media_player: 6,
            scene: 7,
            script: 8,
            input_boolean: 9
        };

        const DOMAIN_CAPABILITIES = {
            light:               { on: 'turn_on', off: 'turn_off', toggle: 'toggle', set: { service: 'turn_on', dataKey: 'brightness_pct' } },
            switch:              { on: 'turn_on', off: 'turn_off', toggle: 'toggle' },
            input_boolean:       { on: 'turn_on', off: 'turn_off', toggle: 'toggle' },
            fan:                 { on: 'turn_on', off: 'turn_off', toggle: 'toggle', set: { service: 'set_percentage', dataKey: 'percentage' } },
            cover:               { on: 'open_cover', off: 'close_cover', toggle: 'toggle', set: { service: 'set_cover_position', dataKey: 'position' } },
            valve:               { on: 'open_valve', off: 'close_valve' },
            climate:             { on: 'turn_on', off: 'turn_off', set: { service: 'set_temperature', dataKey: 'temperature' } },
            water_heater:        { on: 'turn_on', off: 'turn_off', set: { service: 'set_temperature', dataKey: 'temperature' } },
            humidifier:          { on: 'turn_on', off: 'turn_off', set: { service: 'set_humidity', dataKey: 'humidity' } },
            input_number:        { set: { service: 'set_value', dataKey: 'value' } },
            number:              { set: { service: 'set_value', dataKey: 'value' } },
            select:              { set: { service: 'select_option', dataKey: 'option' } },
            input_select:        { set: { service: 'select_option', dataKey: 'option' } },
            media_player:        { on: 'turn_on', off: 'turn_off', toggle: 'toggle' },
            scene:               { on: 'turn_on' },              // scenes only activate
            script:              { on: 'turn_on', off: 'turn_off' },
            automation:          { on: 'turn_on', off: 'turn_off', trigger: 'trigger' },
            vacuum:              { on: 'start', off: 'stop' },
            remote:              { on: 'turn_on', off: 'turn_off' },
            siren:               { on: 'turn_on', off: 'turn_off' },
            button:              { on: 'press' },
            input_button:        { on: 'press' },
            lock:                { on: 'lock', off: 'unlock', confirm: ['off'] },          // unlocking needs confirmation
            alarm_control_panel: { on: 'alarm_arm_away', off: 'alarm_disarm', confirm: ['off'] }
        };

        const FINAL_DEVICE_DOMAINS = new Set([
            'light', 'switch', 'cover', 'climate', 'fan', 'media_player',
            'lock', 'alarm_control_panel'
        ]);

        const INPUT_BOOLEAN_DRIVER_TOKENS = new Set(['state', 'enabled', 'active']);

        const DOMAIN_KEYWORD_HINTS = {
            light: ['light', 'lights', 'lamp', 'lamps', 'lampe', 'lampes', 'lumiere', 'lumieres'],
            switch: ['switch', 'switches', 'prise', 'prises', 'interrupteur', 'machine', 'pump', 'pompe', 'charger'],
            cover: ['door', 'doors', 'blind', 'blinds', 'curtain', 'curtains', 'cover', 'covers', 'garage', 'volet', 'volets', 'rideau', 'rideaux', 'porte', 'portes', 'shutter', 'shutters', 'shade', 'shades', 'store', 'stores'],
            climate: ['thermostat', 'climate', 'chauffage', 'temperature', 'temperatures', 'temp'],
            fan: ['fan', 'fans', 'ventilateur', 'ventilateurs'],
            media_player: ['media', 'player', 'tv', 'television', 'speaker', 'enceinte'],
            lock: ['lock', 'serrure'],
            alarm_control_panel: ['alarm', 'alarme']
        };

        // Generic tokens that show up in many entity_ids and are useless for matching on their own.
        // Kept out of scoring so "kitchen light" doesn't score every light equally on the word "light".
        // 'on' / 'off' added because automation slugs ("..._on_motion") would otherwise score 1
        // just because the user said "turn on" / "turn off".
        const STOP_TOKENS = new Set([
            'light', 'switch', 'scene', 'script', 'input', 'boolean',
            'mode', 'showcase', 'demo', 'test', 'home', 'the', 'a', 'an',
            'sensor', 'binary', 'automation', 'number', 'climate', 'cover',
            'fan', 'media', 'player', 'lock', 'alarm', 'control', 'panel',
            'on', 'off', 'turn', 'set', 'to', 'in', 'of'
        ]);

        // Slug prefixes stripped from the entity_id before tokenization. These add noise without
        // semantic value (every entity in the demo has "showcase_"). Generic to any HA install.
        const SLUG_PREFIXES_TO_STRIP = ['showcase_', 'demo_', 'test_'];

        // Generic synonyms that map a word the user likely says to a token in HA entity ids.
        // These are NOT pins — they just enrich the user's word set before scoring, so a user
        // saying "lampe du salon" matches a light.salon_lamp on any HA install.
        const GENERIC_SYNONYMS = {
            // lights
            'lamp': ['light'], 'lampe': ['light'], 'lumiere': ['light'], 'lumieres': ['light'],
            'luminaire': ['light'], 'plafonnier': ['light', 'ceiling'], 'ceiling': ['light'],
            // switches
            'interrupteur': ['switch'], 'prise': ['switch', 'outlet'], 'outlet': ['switch', 'plug'],
            'plug': ['switch', 'outlet'], 'machine': ['switch'], 'pump': ['switch', 'pump'],
            'pompe': ['switch', 'pump'], 'charger': ['switch', 'charger'],
            // climate / temperature
            'temperature': ['temperature', 'temp', 'climate', 'thermostat'],
            'temp': ['temperature', 'climate'],
            'thermostat': ['climate', 'thermostat'], 'chauffage': ['climate', 'heater', 'heating'],
            'heater': ['climate', 'heater'], 'heating': ['climate', 'heater'],
            'clim': ['climate', 'ac'], 'ac': ['climate', 'ac', 'air'], 'aircon': ['climate'],
            // fans
            'ventilateur': ['fan'], 'fan': ['fan'],
            // covers
            'volet': ['cover', 'shutter'], 'volets': ['cover', 'shutters'], 'shutter': ['cover'],
            'shutters': ['cover'], 'blind': ['cover'], 'blinds': ['cover'],
            'store': ['cover'], 'stores': ['cover'], 'rideau': ['cover', 'curtain'],
            'curtain': ['cover'], 'porte': ['cover', 'door'], 'door': ['cover', 'door'],
            'garage': ['cover', 'garage'], 'shade': ['cover'], 'shades': ['cover'],
            // scenes / scripts / automations
            'scene': ['scene'], 'mode': ['scene', 'input_boolean'], 'routine': ['script', 'scene'],
            // sensors
            'capteur': ['sensor'], 'humidite': ['humidity'], 'humidity': ['humidity'],
            'puissance': ['power'], 'power': ['power'], 'energie': ['energy'], 'energy': ['energy'],
            'batterie': ['battery'], 'battery': ['battery'],
            'qualite': ['quality'], 'quality': ['quality'], 'air': ['air'], 'aqi': ['air', 'quality'],
            'co2': ['co2'], 'pollution': ['air', 'quality'],
            // media
            'tv': ['media', 'tv'], 'television': ['media'], 'speaker': ['media'], 'enceinte': ['media'],
            // common purifier / appliance
            'purifier': ['purifier'], 'purificateur': ['purifier'],
            // locks
            'serrure': ['lock'], 'lock': ['lock'],
            // misc
            'water': ['water'], 'eau': ['water'], 'arrosage': ['valve', 'sprinkler']
        };

        // Friendly-name patterns that strongly suggest "this is an automation rule, not a device".
        // Used to penalize entities like "Showcase: Air purifier on poor air" so they lose to the
        // actual switch/light when the user writes a plain "turn on X" command.
        const AUTOMATION_NAME_HINTS = [
            /on\s+poor\s+air/i,
            /off\s+when/i,
            /on\s+motion/i,
            /\bauto(matic(ally)?|matique(ment)?)?\b/i
        ];

        // ==================================================================================
        // DEMO BONUS LAYER (kept for showcase convenience but never overrides dynamic matches)
        // ----------------------------------------------------------------------------------
        // PINNED_PHRASES adds a small score bonus when the user types a phrase verbatim.
        // MANUAL_ALIASES injects extra tokens into specific demo entities. Both are safe to
        // empty — the resolver works on any HA instance via /api/ha/states + auto-aliases.
        // ==================================================================================
        const PINNED_PHRASES = [
            { phrases: ['air purifier', 'purifier', 'purificateur'],                                entity_id: 'switch.showcase_air_purifier' },
            { phrases: ['hallway light', 'hallway', 'couloir', 'lumiere couloir', 'lampe couloir'], entity_id: 'light.showcase_hallway_light' },
            { phrases: ['kitchen light', 'kitchen', 'cuisine', 'lumiere cuisine', 'lampe cuisine'], entity_id: 'light.showcase_kitchen_light' },
            { phrases: ['living room lamp', 'living room', 'salon', 'lampe salon', 'lampe du salon'], entity_id: 'light.showcase_living_room_lamp' },
            { phrases: ['movie mode', 'mode movie', 'mode cinema', 'cinema mode'],                  entity_id: 'input_boolean.showcase_movie_mode' },
            { phrases: ['sleep mode', 'mode sleep', 'mode nuit', 'mode sommeil', 'sleep', 'sommeil'], entity_id: 'input_boolean.showcase_sleep_mode' }
        ];

        const MANUAL_ALIASES = {
            'switch.showcase_air_purifier':        ['purifier', 'purificateur', 'air'],
            'light.showcase_living_room_lamp':     ['salon', 'lampe', 'living', 'room'],
            'light.showcase_kitchen_light':        ['cuisine', 'kitchen'],
            'light.showcase_hallway_light':        ['couloir', 'hallway', 'hall'],
            'input_boolean.showcase_movie_mode':   ['movie', 'cinema', 'cinéma', 'film'],
            'input_boolean.showcase_sleep_mode':   ['sleep', 'sommeil', 'nuit'],
            'scene.showcase_movie_night':          ['movie', 'film', 'cinema', 'cinéma', 'soiree'],
            'scene.showcase_night':                ['night', 'nuit'],
            'script.showcase_arrive_home':         ['arrive', 'arrived', 'arriver', 'rentre'],
            'script.showcase_leave_home':          ['leave', 'leaving', 'bye', 'depart', 'partir'],
            'script.showcase_good_night':          ['goodnight', 'bonne'],
            'script.showcase_start_laundry':       ['laundry', 'lessive', 'laver'],
            'script.showcase_reset_demo':          ['reset'],
            'script.showcase_start_movie_night':   ['movie', 'film', 'soiree']
        };

        // Normalize a string for matching: lowercase, strip diacritics, collapse whitespace.
        function norm(s) {
            return (s || '').toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/\s+/g, ' ').trim();
        }

        // Tokenize a phrase into atoms suitable for matching against catalog tokens.
        function tokenize(s) {
            return norm(s).split(/[^a-z0-9]+/).filter(Boolean);
        }

        function cleanMatchText(s) {
            return norm(s).replace(/[^a-z0-9]+/g, ' ').replace(/\s+/g, ' ').trim();
        }

        function looksLikeSpecificNordPoolSensorQuery(cleanText) {
            const hasNordPool = /\bnord\s+pool\b|\bnordpool\b/.test(cleanText);
            if (!hasNordPool) return false;
            const mentionsGenericEnergy = /\b(energy|energie|electricity|electricite)\b/.test(cleanText);
            if (mentionsGenericEnergy) return false;
            const hasArea = /\b(no\d+|se\d+|dk\d+|fi|ee|lv|lt)\b/.test(cleanText);
            const hasSlotName = /\b(current|next|previous|lowest|highest|daily|average|peak|off\s+peak|offpeak)\b/.test(cleanText);
            const asksState = /\b(sensor|capteur|state|etat|entity|entite|reading)\b/.test(cleanText);
            return hasSlotName && (hasArea || asksState);
        }

        function isEnergyPriceSummaryQuery(text) {
            const t = cleanMatchText(text);
            if (!t || looksLikeSpecificNordPoolSensorQuery(t)) return false;
            const mentionsPrice = /\b(price|prices|prix|tarif|tarifs|cost|cout|combien)\b/.test(t);
            const mentionsEnergy = /\b(energy|energie|electricity|electricite|nord\s+pool|nordpool)\b/.test(t);
            return mentionsPrice && mentionsEnergy;
        }

        // Strip noisy slug prefixes (showcase_, demo_, test_) without removing meaningful words.
        function stripSlugPrefix(slug) {
            for (const p of SLUG_PREFIXES_TO_STRIP) {
                if (slug.startsWith(p)) return slug.substring(p.length);
            }
            return slug;
        }

        // Build the token set for an entity, dynamically derived from entity_id + friendly_name
        // + device_class + unit + manual aliases + generic synonyms. This is the heart of the
        // portable resolver: any HA instance gets a usable token index without code changes.
        function buildEntityTokens(entity_id, domain, slug, friendly, attrs) {
            const tokens = new Set();
            const addAll = (arr) => { for (const t of arr) if (t) tokens.add(t); };

            // 1. From entity_id slug (e.g. "showcase_air_purifier" → "air", "purifier")
            addAll(tokenize(stripSlugPrefix(slug)));
            // 2. From friendly_name (e.g. "Bedroom Ceiling Light" → "bedroom", "ceiling", "light")
            addAll(tokenize(friendly));
            // 3. From device_class (e.g. "temperature" for sensor.bedroom_temp)
            if (attrs && attrs.device_class) addAll(tokenize(attrs.device_class));
            // 4. From unit_of_measurement (e.g. "°C" → "c"; "kWh" → "kwh") — gives weak hints
            if (attrs && attrs.unit_of_measurement) addAll(tokenize(attrs.unit_of_measurement));
            // 5. From the domain itself (helps "lights" → light.* matches)
            addAll(tokenize(domain));

            // 6. Inject generic synonyms in BOTH directions: if a token like "light" is present,
            //    add "lampe","lumiere"; if "temperature" present, add "temp","thermostat".
            const seedTokens = Array.from(tokens);
            for (const tk of seedTokens) {
                for (const [src, syns] of Object.entries(GENERIC_SYNONYMS)) {
                    if (src === tk || syns.includes(tk)) {
                        if (src) tokens.add(src);
                        for (const s of syns) tokens.add(s);
                    }
                }
            }

            // 7. Demo-aliases as bonus only — they enrich the token set but never override scoring.
            const aliases = MANUAL_ALIASES[entity_id] || [];
            for (const a of aliases) addAll(tokenize(a));

            return tokens;
        }

        // Build a list of "alias phrases" for an entity — the multi-word strings we'll try to
        // match verbatim in the user's text (with word boundaries). Used by the high-confidence
        // exact-phrase score path.
        function buildEntityPhrases(entity_id, domain, slug, friendly) {
            const phrases = new Set();
            const cleanSlug = stripSlugPrefix(slug);
            // entity_id slug as a phrase ("bedroom_ceiling_light" → "bedroom ceiling light")
            const slugPhrase = norm(cleanSlug.replace(/_/g, ' '));
            if (slugPhrase) phrases.add(slugPhrase);
            // friendly_name
            const friendlyPhrase = norm(friendly);
            if (friendlyPhrase) phrases.add(friendlyPhrase);
            // sub-phrases: for "bedroom_ceiling_light", also accept "ceiling light", "bedroom light".
            const parts = cleanSlug.split('_').filter(Boolean);
            if (parts.length >= 2) {
                for (let i = 0; i < parts.length - 1; i++) {
                    phrases.add(norm(parts.slice(i).join(' ')));
                }
            }
            // demo pin phrases — bonus layer
            for (const pin of PINNED_PHRASES) {
                if (pin.entity_id === entity_id) for (const p of pin.phrases) phrases.add(norm(p));
            }
            return phrases;
        }

        // Build the full enriched catalog from the live HA states snapshot.
        // Each entry exposes: entity_id, domain, object_id, friendly_name, device_class,
        // unit_of_measurement, supported_features, state, attributes, tokens (Set), phrases (Set),
        // capabilities (DOMAIN_CAPABILITIES entry), actionable / queryableOnly flags.
        function buildEntityCatalog() {
            const cat = [];
            for (const id of Object.keys(haStates)) {
                const e = haStates[id];
                const dotIdx = id.indexOf('.');
                if (dotIdx < 0) continue;
                const domain = id.substring(0, dotIdx);
                const object_id = id.substring(dotIdx + 1);
                const slug = object_id;
                const attrs = e.attributes || {};
                const friendly = attrs.friendly_name || object_id;
                const tokens = buildEntityTokens(id, domain, slug, friendly, attrs);
                const phrases = buildEntityPhrases(id, domain, slug, friendly);
                cat.push({
                    entity_id: id,
                    domain,
                    object_id,
                    friendly_name: friendly,
                    device_class: attrs.device_class || null,
                    unit_of_measurement: attrs.unit_of_measurement || null,
                    supported_features: attrs.supported_features ?? null,
                    state: e.state,
                    attributes: attrs,
                    tokens,                                                       // Set<string>
                    phrases,                                                      // Set<string>
                    capabilities: DOMAIN_CAPABILITIES[domain] || null,
                    actionable: ACTIONABLE_DOMAINS.has(domain),
                    queryableOnly: QUERYABLE_ONLY_DOMAINS.has(domain)
                });
            }
            entityCatalog = cat;
        }

        // Whole-word substring check (no regex compilation in hot path).
        // Treats anything non-alphanumeric as a boundary so "purifier" matches "the purifier."
        function containsAsWord(haystack, needle) {
            let from = 0;
            while (from <= haystack.length) {
                const idx = haystack.indexOf(needle, from);
                if (idx < 0) return false;
                const left = idx === 0 ? '' : haystack[idx - 1];
                const right = idx + needle.length >= haystack.length ? '' : haystack[idx + needle.length];
                const lb = left === ''  || /[^a-z0-9]/.test(left);
                const rb = right === '' || /[^a-z0-9]/.test(right);
                if (lb && rb) return true;
                from = idx + 1;
            }
            return false;
        }

        function hasAnyToken(words, hints) {
            for (const h of hints || []) {
                if (words.has(h)) return true;
            }
            return false;
        }

        function domainKeywordBonus(domain, words) {
            if (!FINAL_DEVICE_DOMAINS.has(domain)) return 0;
            return hasAnyToken(words, DOMAIN_KEYWORD_HINTS[domain]) ? 1.0 : 0;
        }

        function explicitlyRequestsInputBoolean(normText) {
            return /\b(input[_\s-]?boolean|helper|toggle|boolean)\b/.test(normText);
        }

        function entityCoreTokens(item) {
            const raw = `${stripSlugPrefix(item.object_id || '')} ${item.friendly_name || ''}`;
            const out = new Set();
            for (const tok of tokenize(raw)) {
                if (tok.length < 2) continue;
                if (STOP_TOKENS.has(tok)) continue;
                if (INPUT_BOOLEAN_DRIVER_TOKENS.has(tok)) continue;
                out.add(tok);
            }
            return out;
        }

        function coreNameSimilarity(a, b) {
            const ta = entityCoreTokens(a);
            const tb = entityCoreTokens(b);
            if (!ta.size || !tb.size) return 0;
            let intersection = 0;
            for (const tok of ta) if (tb.has(tok)) intersection++;
            const union = new Set([...ta, ...tb]).size;
            const coverage = intersection / Math.min(ta.size, tb.size);
            const jaccard = intersection / union;
            return Math.max(coverage * 0.7 + jaccard * 0.3, 0);
        }

        function hasSimilarCoreName(a, b) {
            return coreNameSimilarity(a, b) >= 0.78;
        }

        function inputBooleanLooksLikeDriver(item) {
            if (!item || item.domain !== 'input_boolean') return false;
            const nameTokens = tokenize(`${item.object_id || ''} ${item.friendly_name || ''}`);
            return nameTokens.some(t => INPUT_BOOLEAN_DRIVER_TOKENS.has(t));
        }

        function inputBooleanHasFinalEquivalent(item) {
            if (!item || item.domain !== 'input_boolean') return false;
            return entityCatalog.some(other =>
                other !== item &&
                FINAL_DEVICE_DOMAINS.has(other.domain) &&
                hasSimilarCoreName(item, other)
            );
        }

        function finalHasInputBooleanEquivalent(item) {
            if (!item || !FINAL_DEVICE_DOMAINS.has(item.domain)) return false;
            return entityCatalog.some(other =>
                other !== item &&
                other.domain === 'input_boolean' &&
                hasSimilarCoreName(item, other)
            );
        }

        function shouldSuppressInputBooleanCandidate(item, pool, normText) {
            if (!item || item.domain !== 'input_boolean' || explicitlyRequestsInputBoolean(normText || '')) return false;
            const matchingFinalInPool = (pool || []).some(other =>
                other !== item &&
                FINAL_DEVICE_DOMAINS.has(other.domain) &&
                hasSimilarCoreName(item, other)
            );
            if (matchingFinalInPool) return true;
            return inputBooleanHasFinalEquivalent(item);
        }

        function filterClarificationCandidates(candidates, primary, normText) {
            const pool = primary ? [primary, ...candidates] : candidates;
            const filtered = candidates.filter(c => !shouldSuppressInputBooleanCandidate(c, pool, normText));
            return filtered.length ? filtered : candidates;
        }

        function clampCoverPosition(value) {
            return Math.max(0, Math.min(100, value));
        }

        function hasCoverIntent(text) {
            const words = new Set(tokenize(text));
            return hasAnyToken(words, DOMAIN_KEYWORD_HINTS.cover) ||
                   /\b(open|ouvre|ouvrir|close|ferme|fermer)\b/.test(text);
        }

        function parseCoverPosition(text) {
            const t = norm(text);
            if (!hasCoverIntent(t)) return null;
            const m = t.match(/\b(?:set|put|mets|mettre|regle|regler|change|tourne|open|ouvre|ouvrir|close|ferme|fermer)\b.*?(?:to|a|at|au|en|position)?\s*(\d{1,3})\s*(?:%|percent|pourcent)?\b/);
            if (!m) return null;
            return clampCoverPosition(parseInt(m[1], 10));
        }

        function buildSetServiceFromEntity(entity, numericVal, source) {
            if (!entity || !entity.capabilities || !entity.capabilities.set) return null;
            const cap = entity.capabilities.set;
            const value = entity.domain === 'cover' ? clampCoverPosition(numericVal) : numericVal;
            return {
                intent: 'call_service', source: source || 'keyword',
                domain: entity.domain, service: cap.service,
                entity_id: entity.entity_id,
                data: { entity_id: entity.entity_id, [cap.dataKey]: value },
                answer: `OK - ${entity.friendly_name} set to ${value}.`
            };
        }

        function isTemperatureReadQuery(text) {
            const t = norm(text);
            return /\b(temp|temperature|temperatures)\b/.test(t);
        }

        function explicitlyRequestsClimateReading(text) {
            const t = norm(text);
            return /\b(thermostat|climate|chauffage|target|consigne)\b/.test(t);
        }

        function resolveTemperatureSensor(text) {
            return resolveEntity(text, {
                queryable: true,
                includedDomains: ['sensor'],
                requiredDeviceClass: 'temperature',
                preferredDomains: ['sensor'],
                excludedDomains: ['automation'],
                allowAutomation: false,
                skipPins: true
            });
        }

        // Classify the user utterance into a command kind, which drives:
        //   - which domains we prefer (light/switch beat input_boolean for direct commands)
        //   - whether automations are eligible at all
        //   - whether pinned phrases apply (we skip pins when user explicitly wants an automation)
        function classifyCommand(text) {
            const normCommand = norm(text);
            const wantsCoverAction = /\b(open|ouvre|ouvrir|close|ferme|fermer)\b/.test(normCommand) && hasCoverIntent(normCommand);
            const wantsSetValue = /\b(set|put|mets|mettre|regle|regler|change|tourne)\b/.test(normCommand) && /\d/.test(normCommand);
            const t = text.toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g, '');
            // Explicit automation request — only opens tier-3 entities.
            if (/\b(automation|automatisation|trigger|run\s+the\s+rule|active\s+la\s+regle)\b/.test(t)) {
                return {
                    kind: 'automation',
                    preferredDomains: ['automation', 'script'],
                    excludedDomains: [],
                    allowAutomation: true,
                    skipPins: true
                };
            }
            // "Mode" command — input_boolean takes priority over scene; automation excluded.
            if (/\bmode\b/.test(t)) {
                return {
                    kind: 'mode',
                    preferredDomains: ['input_boolean', 'scene', 'script'],
                    excludedDomains: ['automation'],
                    allowAutomation: false,
                    skipPins: false
                };
            }
            // Covers use open/close verbs instead of Home Assistant turn_on/turn_off.
            if (wantsCoverAction) {
                return {
                    kind: 'cover',
                    preferredDomains: ['cover', 'lock', 'light', 'switch', 'fan', 'media_player', 'climate', 'input_boolean'],
                    excludedDomains: ['automation'],
                    allowAutomation: false,
                    skipPins: false
                };
            }
            // Numeric setters should prefer native setpoint/value domains before booleans.
            if (wantsSetValue) {
                return {
                    kind: 'set_value',
                    preferredDomains: ['climate', 'cover', 'fan', 'number', 'input_number', 'light', 'switch', 'media_player', 'input_boolean'],
                    excludedDomains: ['automation'],
                    allowAutomation: false,
                    skipPins: false
                };
            }
            // Default: a direct device command (turn on/off X).
            return {
                kind: 'device',
                preferredDomains: ['light', 'switch', 'cover', 'climate', 'fan', 'media_player', 'lock', 'alarm_control_panel', 'input_boolean', 'scene', 'script'],
                excludedDomains: ['automation'],
                allowAutomation: false,
                skipPins: false
            };
        }

        // Resolve a free-text user utterance to an entity in the catalog.
        //
        // Generic, portable logic — works on ANY HA instance:
        //   (1) Score every candidate on multiple signals:
        //         + exact alias-phrase match              (very strong; e.g. "kitchen ceiling light")
        //         + token overlap                        (per non-stop token)
        //         + friendly_name word match             (extra weight)
        //         + device_class match                   (e.g. "temperature" → sensor with device_class=temperature)
        //         + domain-tier bonus / preferred-domain bonus
        //         − stop-token weak weight
        //         − automation-name penalty when not asked for an automation
        //   (2) Demo pins (PINNED_PHRASES) add a small bonus when the user types one of their phrases.
        //       They never override a stronger dynamic match — they just nudge ties toward the demo entity.
        //
        // opts:
        //   actionableOnly: bool       — restrict to ACTIONABLE_DOMAINS (set true for control commands)
        //   queryable: bool            — include sensors/binary_sensor/weather/... (set true for state queries)
        //   preferredDomains: string[] — bonus if the entity's domain is in this list
        //   excludedDomains: string[]  — entities in these domains are dropped before scoring
        //   allowAutomation: bool      — when false, all 'automation' entities are dropped
        //   skipPins: bool             — when true, ignore demo pins entirely
        //
        // Returns:
        //   { match: 'none', candidates: [] }
        //   { match: 'one', entity, score, alternatives: [...] }
        //   { match: 'multiple', candidates: [...] }
        function resolveEntity(text, opts) {
            opts = opts || {};
            const normText = norm(text);
            const words = new Set(tokenize(text));
            // Inject generic synonyms into the user's word set so "lampe" hits "light".
            for (const w of Array.from(words)) {
                const syns = GENERIC_SYNONYMS[w];
                if (syns) for (const s of syns) words.add(s);
            }
            const preferredDomains = opts.preferredDomains || [];
            const preferred = new Set(preferredDomains);
            const excluded = new Set(opts.excludedDomains || []);
            const included = opts.includedDomains ? new Set(opts.includedDomains) : null;
            const requiredDeviceClass = opts.requiredDeviceClass || null;
            const allowAutomation = opts.allowAutomation === true;
            const queryable = opts.queryable === true;
            const explicitInputBoolean = explicitlyRequestsInputBoolean(normText);

            // Demo pins — collect phrase hits as a soft bonus layer.
            const pinHits = new Set();
            if (!opts.skipPins) {
                for (const pin of PINNED_PHRASES) {
                    for (const phrase of pin.phrases) {
                        if (containsAsWord(normText, norm(phrase))) {
                            pinHits.add(pin.entity_id);
                            break;
                        }
                    }
                }
            }

            const scored = [];
            for (const item of entityCatalog) {
                if (opts.actionableOnly && !item.actionable && !(queryable && item.queryableOnly)) continue;
                if (excluded.has(item.domain)) continue;
                if (included && !included.has(item.domain)) continue;
                if (requiredDeviceClass && (item.device_class || '').toLowerCase() !== requiredDeviceClass) continue;
                if (item.domain === 'automation' && !allowAutomation) continue;

                // (a) token overlap
                let raw = 0, weak = 0;
                for (const tok of item.tokens) {
                    if (!words.has(tok)) continue;
                    if (STOP_TOKENS.has(tok)) weak += 0.15;
                    else raw += 1;
                }
                // (b) exact alias-phrase match — strongest signal
                let phraseBonus = 0;
                for (const phrase of item.phrases) {
                    if (phrase && phrase.length >= 3 && containsAsWord(normText, phrase)) {
                        phraseBonus = Math.max(phraseBonus, Math.min(3.0, 0.6 + phrase.split(' ').length * 0.5));
                    }
                }
                // (c) friendly_name token-match — extra weight (the user often says it verbatim)
                let friendlyBonus = 0;
                for (const ft of tokenize(item.friendly_name)) {
                    if (!STOP_TOKENS.has(ft) && words.has(ft)) friendlyBonus += 0.4;
                }
                // (d) device_class match — gold for "what is the kitchen temperature"
                let dcBonus = 0;
                if (item.device_class && words.has(item.device_class.toLowerCase())) dcBonus += 1.2;
                // Need at least one signal to be considered.
                if (raw === 0 && phraseBonus === 0 && friendlyBonus === 0 && dcBonus === 0) continue;

                // (e) tier + preferred-domain shaping
                const tier = DOMAIN_TIERS[item.domain] || 4;
                const tierBonus = tier === 1 ? 0.6 : tier === 2 ? 0.0 : -0.6;
                const prefIndex = preferredDomains.indexOf(item.domain);
                const prefBonus = preferred.has(item.domain) ? Math.max(0.45, 1.6 - prefIndex * 0.08) : 0;
                const domainBonus = domainKeywordBonus(item.domain, words);
                // (f) automation-name penalty when not asked for an automation
                let namePenalty = 0;
                if (!allowAutomation) {
                    for (const re of AUTOMATION_NAME_HINTS) {
                        if (re.test(item.friendly_name)) { namePenalty -= 2.0; break; }
                    }
                }
                // (g) helper-device disambiguation and demo pin soft bonus
                let inputBooleanPenalty = 0;
                if (item.domain === 'input_boolean' && !explicitInputBoolean) {
                    if (inputBooleanLooksLikeDriver(item)) inputBooleanPenalty -= 1.2;
                    if (inputBooleanHasFinalEquivalent(item)) inputBooleanPenalty -= 1.6;
                }
                const finalEquivalentBonus = finalHasInputBooleanEquivalent(item) ? 0.35 : 0;
                const explicitInputBooleanBonus = (item.domain === 'input_boolean' && explicitInputBoolean) ? 1.0 : 0;
                const pinBonus = pinHits.has(item.entity_id) ? 0.4 : 0;

                scored.push({
                    item,
                    score: raw + weak + phraseBonus + friendlyBonus + dcBonus + tierBonus + prefBonus + domainBonus + namePenalty + inputBooleanPenalty + finalEquivalentBonus + explicitInputBooleanBonus + pinBonus
                });
            }
            if (!scored.length) return { match: 'none', candidates: [] };
            scored.sort((a, b) => b.score - a.score);
            const top = scored[0];
            const ties = scored.filter(s => Math.abs(s.score - top.score) < 0.05);
            if (ties.length > 1) {
                const filteredTies = filterClarificationCandidates(ties.map(s => s.item), null, normText);
                if (filteredTies.length === 1) {
                    const only = filteredTies[0];
                    const onlyScore = (scored.find(s => s.item === only) || top).score;
                    const alternatives = filterClarificationCandidates(scored.filter(s => s.item !== only).slice(0, 6).map(s => s.item), only, normText).slice(0, 3);
                    return { match: 'one', entity: only, score: onlyScore, alternatives };
                }
                return { match: 'multiple', candidates: filteredTies };
            }
            // Expose runner-ups so the UI can offer "did you mean..." even on a clean win.
            const alternatives = filterClarificationCandidates(scored.slice(1, 7).map(s => s.item), top.item, normText).slice(0, 3);
            return { match: 'one', entity: top.item, score: top.score, alternatives };
        }

        // Resolve the right HA service to call for a domain + action verb.
        // Driven entirely by DOMAIN_CAPABILITIES, so adding a new domain is a one-line edit.
        // verb ∈ 'on' | 'off' | 'toggle' | 'set' | 'trigger'
        // Returns { service, dataKey?, requiresConfirmation } or null when unsupported.
        function serviceForDomain(domain, verb) {
            const caps = DOMAIN_CAPABILITIES[domain];
            if (!caps) return null;
            const v = verb || 'on';
            if (v === 'set' && caps.set) {
                return { service: caps.set.service, dataKey: caps.set.dataKey, requiresConfirmation: false };
            }
            const svc = caps[v];
            if (!svc) return null;
            const requiresConfirmation = Array.isArray(caps.confirm) && caps.confirm.includes(v);
            return { service: svc, requiresConfirmation };
        }

        // Legacy ruleless actuator names (pre-call_service era) → real HA entity IDs.
        // The LLM occasionally still emits intent="actuate" with these names; we use this
        // map to translate them so the guard can compare with what the user actually asked for.
        const LEGACY_TARGET_TO_ENTITY = {
            'LivingRoomLight':  'light.showcase_living_room_lamp',
            'KitchenLight':     'light.showcase_kitchen_light',
            'HallwayLight':     'light.showcase_hallway_light',
            'AirPurifier':      'switch.showcase_air_purifier'
        };

        // Build a corrected call_service intent from a catalog entity + user-text turn-off hint.
        function buildCallServiceFromEntity(entity, userText) {
            const coverPosition = entity && entity.domain === 'cover' ? parseCoverPosition(userText) : null;
            if (coverPosition != null) {
                const setCall = buildSetServiceFromEntity(entity, coverPosition, 'guard');
                if (setCall) return setCall;
            }
            const turnOff = /\b(off|eteint|eteins|eteindre|coupe|couper|stop|disable|deactivate|kill|arrete|arreter|ferme|fermer|close)\b/i
                                .test(norm(userText));
            const verb = turnOff ? 'off' : 'on';
            const resolved = serviceForDomain(entity.domain, verb);
            if (!resolved) return { intent: 'cant_turn_off', entity, source: 'guard' };
            return {
                intent: 'call_service', source: 'guard',
                domain: entity.domain, service: resolved.service,
                entity_id: entity.entity_id,
                data: { entity_id: entity.entity_id },
                requiresConfirmation: resolved.requiresConfirmation,
                answer: `OK — ${entity.friendly_name}: ${entity.domain}.${resolved.service}.`
            };
        }

        function formatCoverServiceMessage(nlu, payloadData) {
            if (!nlu || nlu.domain !== 'cover') return null;
            const entityId = nlu.entity_id || (payloadData && payloadData.entity_id);
            const cat = entityId ? entityCatalog.find(e => e.entity_id === entityId) : null;
            const name = cat ? cat.friendly_name : (entityId || 'cover');
            const serviceLabel = `cover.${nlu.service}`;
            if (nlu.service === 'open_cover') return `OK — ${name} opened. <span style="color:var(--text-muted)">(${serviceLabel} → HA)</span>`;
            if (nlu.service === 'close_cover') return `OK — ${name} closed. <span style="color:var(--text-muted)">(${serviceLabel} → HA)</span>`;
            if (nlu.service === 'set_cover_position') {
                const pos = payloadData && payloadData.position != null ? clampCoverPosition(parseInt(payloadData.position, 10)) : null;
                const posText = pos == null || isNaN(pos) ? 'requested position' : `${pos}%`;
                return `OK — ${name} set to ${posText}. <span style="color:var(--text-muted)">(${serviceLabel} → HA)</span>`;
            }
            return null;
        }

        // Guardrail: validate any LLM-produced action intent against the user's text.
        // Even if Ollama hallucinates LivingRoomLight for "turn on purifier", the local resolver
        // catches the disagreement and overrides. Non-action intents (price/query/optimize/...)
        // pass through untouched.
        function guardEntityChoice(nlu, userText) {
            if (!nlu || nlu.source !== 'llm') return nlu; // keyword fallback already used resolveEntity

            const isCallService  = nlu.intent === 'call_service';
            const isLegacyActuate = nlu.intent === 'actuate' && nlu.target && LEGACY_TARGET_TO_ENTITY[nlu.target];
            if (!isCallService && !isLegacyActuate) return nlu;

            // Allow domain-wide broadcasts ("all lights off") to bypass the guard.
            const broadcast = isCallService && (nlu.entity_id === 'all' ||
                                                (nlu.data && nlu.data.entity_id === 'all'));
            if (broadcast) return nlu;

            // What entity did the LLM target?
            let llmEntity = null;
            if (isCallService)       llmEntity = nlu.entity_id || (nlu.data && nlu.data.entity_id) || null;
            else if (isLegacyActuate) llmEntity = LEGACY_TARGET_TO_ENTITY[nlu.target];
            const llmEntCat = llmEntity ? entityCatalog.find(e => e.entity_id === llmEntity) : null;

            // Use the same command-kind classification as the keyword fallback so that
            // the guard's resolveEntity sees the same domain preferences (avoids automations
            // being treated as legitimate candidates for a plain "turn on X" command).
            const cmd = classifyCommand(userText);

            // Hard reject: LLM proposed an automation for a non-automation command.
            // This catches "turn on purifier → automation.showcase_air_purifier_on_poor_air".
            if (llmEntCat && llmEntCat.domain === 'automation' && cmd.kind !== 'automation') {
                const local = resolveEntity(userText, { actionableOnly: true, ...cmd });
                if (local.match === 'one') {
                    console.warn('[guard] LLM picked automation', llmEntity, 'for', cmd.kind, 'command → overriding with', local.entity.entity_id);
                    return buildCallServiceFromEntity(local.entity, userText);
                }
                if (local.match === 'multiple') {
                    return { intent: 'clarify', candidates: local.candidates, original: userText, source: 'guard' };
                }
                console.warn('[guard] LLM picked automation', llmEntity, 'for', cmd.kind, 'command and no local match → unknown');
                return { intent: 'unknown_entity', original: userText, source: 'guard' };
            }

            // What does the user text resolve to locally, with the same preferences?
            const local = resolveEntity(userText, { actionableOnly: true, ...cmd });

            if (local.match === 'one') {
                const expected = buildCallServiceFromEntity(local.entity, userText);
                if (llmEntity !== local.entity.entity_id) {
                    return expected;
                }
                if (isLegacyActuate) return expected;
                if (isCallService && expected.intent === 'call_service') {
                    const llmData = nlu.data || {};
                    const sameService = nlu.domain === expected.domain && nlu.service === expected.service;
                    const sameData = Object.keys(expected.data || {}).every(k => {
                        const actual = k === 'entity_id' ? (llmData.entity_id || nlu.entity_id) : llmData[k];
                        return String(actual) === String(expected.data[k]);
                    });
                    if (!sameService || !sameData) return expected;
                }
                return nlu;
            }

            if (local.match === 'multiple') {
                // Trust the LLM only if its pick is among the local candidates AND not an automation.
                if (llmEntity && local.candidates.some(c => c.entity_id === llmEntity)) {
                    if (isLegacyActuate) {
                        const ent = entityCatalog.find(e => e.entity_id === llmEntity);
                        if (ent) return buildCallServiceFromEntity(ent, userText);
                    }
                    return nlu;
                }
                return { intent: 'clarify', candidates: local.candidates, original: userText, source: 'guard' };
            }

            // local.match === 'none'
            if (llmEntity && haStates[llmEntity]) {
                const lr = /\b(living\s*room|salon|sofa|couch|tv|lamp|lampe)\b/i.test(userText);
                if (llmEntity === 'light.showcase_living_room_lamp' && !lr) {
                    console.warn('[guard] LLM defaulted to living-room lamp without text justification → rejecting');
                    return { intent: 'unknown_entity', original: userText, source: 'guard' };
                }
                if (isLegacyActuate) {
                    const ent = entityCatalog.find(e => e.entity_id === llmEntity);
                    if (ent) return buildCallServiceFromEntity(ent, userText);
                }
                return nlu;
            }
            return { intent: 'unknown_entity', original: userText, source: 'guard' };
        }

        function numberOrNull(value) {
            const n = typeof value === 'number' ? value : parseFloat(value);
            return Number.isFinite(n) ? n : null;
        }

        function normalizePriceList(prices) {
            return Array.isArray(prices)
                ? prices.map(numberOrNull).filter(v => v != null)
                : [];
        }

        function escapeHtml(value) {
            return String(value ?? '').replace(/[&<>"']/g, ch => ({
                '&': '&amp;',
                '<': '&lt;',
                '>': '&gt;',
                '"': '&quot;',
                "'": '&#39;'
            }[ch]));
        }

        function setText(el, value) {
            if (el) el.textContent = value;
        }

        function setDataBadge(el, text, state) {
            if (!el) return;
            el.className = `data-badge ${state || 'warn'}`;
            el.textContent = text;
        }

        function setModelHealthBadge(text, state) {
            if (!modelHealthBadge) return;
            modelHealthBadge.className = `data-badge ${state || 'muted'} model-health-badge`;
            modelHealthBadge.textContent = text;
        }

        function modelHealthStateFor(result) {
            switch (String(result || '').toUpperCase()) {
                case 'PASS': return 'ok';
                case 'WARN': return 'warn';
                case 'FAIL': return 'fail';
                default: return 'muted';
            }
        }

        function inferModelHealthResult(data, httpStatus) {
            const explicit = String(data && data.result ? data.result : '').toUpperCase();
            if (explicit) return explicit;
            const errors = Number(data && data.errorCount);
            const warnings = Number(data && data.warningCount);
            if (Number.isFinite(errors) && errors > 0) return 'FAIL';
            if (httpStatus === 422) return 'FAIL';
            if (Number.isFinite(warnings) && warnings > 0) return 'WARN';
            if (httpStatus >= 200 && httpStatus < 300 && data && !data.error) return 'PASS';
            return 'UNAVAILABLE';
        }

        function formatReachability(reachable, checked) {
            const checkedNumber = Number(checked);
            const reachableNumber = Number(reachable);
            if (!Number.isFinite(checkedNumber)) return '-';
            return `${Number.isFinite(reachableNumber) ? reachableNumber : 0}/${checkedNumber}`;
        }

        function issueText(issue) {
            if (!issue) return '';
            if (typeof issue === 'string') return issue;
            const code = issue.code ? `[${issue.code}] ` : '';
            return `${code}${issue.message || issue.error || ''}`.trim();
        }

        function renderModelHealthIssues(errors, warnings) {
            if (!modelHealthIssues) return;
            const errorItems = Array.isArray(errors) ? errors : [];
            const warningItems = Array.isArray(warnings) ? warnings : [];
            const combined = [
                ...errorItems.map(issue => ({ type: 'error', text: issueText(issue) })),
                ...warningItems.map(issue => ({ type: 'warning', text: issueText(issue) }))
            ].filter(item => item.text);
            const visible = combined.slice(0, 3);
            const more = combined.length - visible.length;
            modelHealthIssues.innerHTML = [
                ...visible.map(item =>
                    `<div class="model-health-issue ${item.type === 'error' ? 'error' : ''}">${escapeHtml(item.text)}</div>`),
                more > 0 ? `<div class="model-health-issue">+${more} more</div>` : ''
            ].join('');
        }

        function renderModelHealth(data, httpStatus = 200) {
            modelHealthSnapshot = data || {};
            const result = inferModelHealthResult(modelHealthSnapshot, httpStatus);
            const badgeState = modelHealthStateFor(result);
            const errorCount = Number.isFinite(Number(modelHealthSnapshot.errorCount)) ? Number(modelHealthSnapshot.errorCount) : 0;
            const warningCount = Number.isFinite(Number(modelHealthSnapshot.warningCount)) ? Number(modelHealthSnapshot.warningCount) : 0;
            const errorMessage = modelHealthSnapshot.error ? String(modelHealthSnapshot.error) : '';

            setModelHealthBadge(result === 'UNAVAILABLE' ? 'Unavailable' : result, badgeState);
            setText(modelHealthSummary, errorMessage || `${result}: ${errorCount} errors, ${warningCount} warnings`);
            setText(modelHealthProfile, modelHealthSnapshot.profile || 'Not reported');
            setText(modelHealthSource, modelHealthSnapshot.source || (errorMessage ? 'Not configured' : 'Not reported'));
            setText(modelHealthSensors, modelHealthSnapshot.sensors ?? '-');
            setText(modelHealthActuators, modelHealthSnapshot.actuators ?? '-');
            setText(modelHealthErrors, errorCount);
            setText(modelHealthWarnings, warningCount);
            setText(modelHealthMode, modelHealthSnapshot.mode || 'offline');
            setText(modelHealthLive,
                modelHealthSnapshot.checksLiveHomeAssistant === false
                    ? 'No (offline only)'
                    : modelHealthSnapshot.checksLiveHomeAssistant === true
                        ? 'Yes'
                        : 'Not reported');
            setText(modelHealthEntities, formatReachability(
                modelHealthSnapshot.haEntitiesReachable,
                modelHealthSnapshot.haEntitiesChecked));
            setText(modelHealthServices, formatReachability(
                modelHealthSnapshot.haServicesReachable,
                modelHealthSnapshot.haServicesChecked));
            renderModelHealthIssues(modelHealthSnapshot.errors, modelHealthSnapshot.warnings);
        }

        function renderModelHealthUnavailable(message, live = false) {
            setModelHealthBadge('Unavailable', 'fail');
            setText(modelHealthSummary, message || 'Could not read /api/model/validation');
            setText(modelHealthProfile, 'Unavailable');
            setText(modelHealthSource, 'Unavailable');
            setText(modelHealthSensors, '-');
            setText(modelHealthActuators, '-');
            setText(modelHealthErrors, '-');
            setText(modelHealthWarnings, '-');
            setText(modelHealthMode, live ? 'live' : 'offline');
            setText(modelHealthLive, live ? 'Requested; unavailable' : 'No (offline only)');
            setText(modelHealthEntities, '-');
            setText(modelHealthServices, '-');
            if (modelHealthIssues) {
                modelHealthIssues.innerHTML = `<div class="model-health-issue error">${escapeHtml(message || 'Model health unavailable.')}</div>`;
            }
        }

        async function refreshModelHealth(live = false) {
            if (modelHealthRefresh) modelHealthRefresh.disabled = true;
            if (modelHealthLiveRefresh) modelHealthLiveRefresh.disabled = true;
            if (live && modelHealthLiveRefresh) modelHealthLiveRefresh.textContent = 'Checking...';
            if (!live && modelHealthRefresh) modelHealthRefresh.textContent = 'Checking...';
            try {
                const r = await fetch(`${API}/api/model/validation${live ? '?live=true' : ''}`);
                const text = await r.text();
                let data = {};
                try { data = text ? JSON.parse(text) : {}; }
                catch (_) { data = { mode: live ? 'live' : 'offline', error: text || `HTTP ${r.status}`, checksLiveHomeAssistant: live }; }

                if (r.status === 400 || r.status >= 500) {
                    renderModelHealth({
                        ...data,
                        result: 'UNAVAILABLE',
                        error: data.error || `HTTP ${r.status}`,
                        checksLiveHomeAssistant: live
                    }, r.status);
                    return;
                }

                if (!r.ok && r.status !== 422) {
                    renderModelHealthUnavailable(data.error || `HTTP ${r.status}`, live);
                    return;
                }

                renderModelHealth(data, r.status);
            } catch (e) {
                renderModelHealthUnavailable(e && e.message ? e.message : 'Network error while checking model health.', live);
            } finally {
                if (modelHealthRefresh) {
                    modelHealthRefresh.disabled = false;
                    modelHealthRefresh.textContent = 'Refresh';
                }
                if (modelHealthLiveRefresh) {
                    modelHealthLiveRefresh.disabled = false;
                    modelHealthLiveRefresh.textContent = 'Live check';
                }
            }
        }

        function formatClock(date) {
            return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
        }

        function formatPriceNumber(value) {
            const n = numberOrNull(value);
            return n == null ? null : n.toFixed(3);
        }

        function formatPriceAmount(value, unit) {
            const n = formatPriceNumber(value);
            return n == null ? null : `${n} ${unit}`;
        }

        function formatPriceRangeAmount(low, high, unit, html = false) {
            const lowText = formatPriceNumber(low);
            const highText = formatPriceNumber(high);
            if (lowText == null || highText == null) return null;
            const separator = html ? '&ndash;' : '-';
            const safeUnit = html ? escapeHtml(unit) : unit;
            return `${lowText}${separator}${highText} ${safeUnit}`;
        }

        function humanizeForecastSource(source) {
            const s = String(source || '').trim();
            if (!s) return 'Home Assistant Nord Pool';
            if (/homeassistant:nordpool/i.test(s) || /nordpool\.get_prices/i.test(s)) {
                return 'Home Assistant Nord Pool';
            }
            return s.replace(/[_:]+/g, ' ');
        }

        function buildForecastSourceLabel(forecast, unit) {
            if (!forecast) return '';
            const area = String(forecast.area || '').trim().toUpperCase();
            const currency = String(forecast.currency || '').trim().toUpperCase();
            const rawSource = String(forecast.forecastSource || forecast.source || '').trim();
            if (!rawSource && !area && !currency) return '';
            const source = humanizeForecastSource(rawSource);
            const parts = [];
            if (source) parts.push(source);
            if (area && !source.toUpperCase().includes(area)) parts.push(area);
            let label = parts.join(' ').trim();
            if (currency && !String(unit || '').toUpperCase().includes(currency) && !label.toUpperCase().includes(currency)) {
                label = `${label} ${currency}`.trim();
            }
            return label;
        }

        function updatePriceStatus(snapshot, error) {
            if (error) {
                setText(energySourceStatus, 'Price feed unavailable');
                setText(energySourceHint, error.message || 'SmartNode could not read /api/price');
                setText(dashEnergy, 'Unavailable');
                setText(dashEnergyHint, 'Check /api/price');
                setDataBadge(nordpoolBadge, 'Nord Pool unavailable', 'warn');
                return;
            }

            const data = snapshot || priceSnapshot || {};
            const prices = normalizePriceList(data.prices);
            const unit = String(data.unit || '').trim() || 'NOK/kWh';
            const current = numberOrNull(data.current) ?? (prices.length ? prices[0] : null);
            const forecast = data.forecast || {};
            const sourceLabel = buildForecastSourceLabel(forecast, unit) || 'Home Assistant Nord Pool';
            const area = String(forecast.area || data.area || '').trim().toUpperCase();
            const shortArea = area || (sourceLabel.match(/\bNO\d+\b/i) || [''])[0].toUpperCase();

            if (current == null && !prices.length) {
                setText(energySourceStatus, 'Waiting for price data');
                setText(energySourceHint, 'No price slots returned yet');
                setText(dashEnergy, 'Waiting');
                setText(dashEnergyHint, 'Nord Pool price');
                setDataBadge(nordpoolBadge, shortArea ? `Nord Pool ${shortArea} pending` : 'Nord Pool pending', 'warn');
                return;
            }

            setText(energySourceStatus, sourceLabel);
            setText(energySourceHint, `${prices.length || 1} price slots loaded`);
            setText(dashEnergy, formatPriceAmount(current, unit) || 'Available');
            setText(dashEnergyHint, shortArea ? `Nord Pool ${shortArea}` : 'Nord Pool price');
            setDataBadge(nordpoolBadge, shortArea ? `Nord Pool ${shortArea}` : 'Nord Pool available', 'ok');
        }

        async function fetchPriceSnapshot() {
            const r = await fetch(`${API}/api/price`);
            if (!r.ok) {
                const body = await r.text().catch(() => '');
                throw new Error(`HTTP ${r.status}${body ? ': ' + body.slice(0, 120) : ''}`);
            }
            const j = await r.json();
            priceSnapshot = j || {};
            energyPrices = normalizePriceList(priceSnapshot.prices);
            updatePriceStatus(priceSnapshot);
            return priceSnapshot;
        }

        async function refreshPrices() {
            try {
                await fetchPriceSnapshot();
            } catch (e) {
                updatePriceStatus(null, e);
            }
        }

        function renderNordPoolPriceSummary(snapshot) {
            const data = snapshot || priceSnapshot || {};
            const prices = normalizePriceList(data.prices);
            const unit = String(data.unit || '').trim() || 'NOK/kWh';
            const current = numberOrNull(data.current) ?? (prices.length ? prices[0] : null);
            const next = numberOrNull(data.next) ?? (prices.length > 1 ? prices[1] : null);
            const lowest = numberOrNull(data.lowest) ?? (prices.length ? Math.min(...prices) : null);
            const highest = numberOrNull(data.highest) ?? (prices.length ? Math.max(...prices) : null);
            const dailyAverage = numberOrNull(data.dailyAverage) ??
                (prices.length ? prices.reduce((sum, p) => sum + p, 0) / prices.length : null);
            const forecast = data.forecast || {};
            const warning = forecast.warning || data.warning || '';

            if (current == null && !prices.length) {
                return warning
                    ? `Prices unavailable: ${escapeHtml(warning)}`
                    : 'Prices unavailable.';
            }

            const lines = [];
            const currentText = formatPriceAmount(current, unit);
            const nextText = formatPriceAmount(next, unit);
            const rangeText = formatPriceRangeAmount(lowest, highest, unit, true);
            const avgText = formatPriceAmount(dailyAverage, unit);
            const sourceLabel = buildForecastSourceLabel(forecast, unit);

            if (currentText) lines.push(`Current Nord Pool price: <b>${escapeHtml(currentText)}</b>.`);
            if (nextText) lines.push(`Next slot: <b>${escapeHtml(nextText)}</b>.`);
            if (rangeText) lines.push(`Today's range: <b>${rangeText}</b>.`);
            if (avgText) lines.push(`Daily average: <b>${escapeHtml(avgText)}</b>.`);

            const forecastUnavailable = forecast.forecastAvailable === false || data.forecastAvailable === false;
            if (forecastUnavailable) {
                lines.push(`Forecast warning: ${escapeHtml(warning || 'future Nord Pool forecast unavailable')}.`);
            } else if (sourceLabel) {
                lines.push(`Forecast source: ${escapeHtml(sourceLabel)}.`);
            }

            const metric = (label, value) => value
                ? `<div class="energy-metric"><span>${label}</span><b>${value}</b></div>`
                : '';
            const metrics = [
                metric('Current', currentText ? escapeHtml(currentText) : ''),
                metric('Next slot', nextText ? escapeHtml(nextText) : ''),
                metric('Range today', rangeText || ''),
                metric('Daily average', avgText ? escapeHtml(avgText) : '')
            ].filter(Boolean).join('');
            const plainText = lines.map(line => line.replace(/<[^>]+>/g, '').replace(/&ndash;/g, '-')).join(' ');

            return `
                <article class="energy-price-card" aria-label="${escapeHtml(plainText)}">
                    <div class="energy-card-head">
                        <div class="energy-card-title">Energy price</div>
                        <div class="energy-card-source">${escapeHtml(sourceLabel || 'Nord Pool')}</div>
                    </div>
                    <div class="energy-metrics">${metrics}</div>
                    <div class="energy-lines">${lines.join('<br>')}</div>
                </article>
            `;
        }

        function renderPriceUnavailable(error) {
            const detail = error && error.message ? `: ${escapeHtml(error.message)}` : '';
            return `Prices unavailable${detail}.`;
        }

        // Pick the first entity matching a (domain, device_class) preference, with a fallback
        // to a slug substring. Used to populate the dashboard dynamically on any HA instance.
        function findFirstByDeviceClass(domain, deviceClass, slugFallback) {
            // 1. exact device_class match
            for (const e of entityCatalog) {
                if (e.domain === domain && (e.device_class || '').toLowerCase() === deviceClass) return e;
            }
            // 2. slug fallback
            if (slugFallback) {
                for (const e of entityCatalog) {
                    if (e.domain === domain && e.object_id.includes(slugFallback)) return e;
                }
            }
            return null;
        }

        function updateDashboard() {
            // Temperature: prefer climate.current_temperature, fall back to sensor device_class.
            let tempStr = '-';
            const climateEnt = entityCatalog.find(e => e.domain === 'climate' && e.attributes && e.attributes.current_temperature != null);
            if (climateEnt) {
                tempStr = `${parseFloat(climateEnt.attributes.current_temperature).toFixed(1)} C`;
            } else {
                const t = findFirstByDeviceClass('sensor', 'temperature', 'temp');
                if (t && t.state != null && !isNaN(parseFloat(t.state))) tempStr = `${parseFloat(t.state).toFixed(1)} ${t.unit_of_measurement || 'C'}`;
            }
            const p = findFirstByDeviceClass('sensor', 'power', 'power');
            const a = findFirstByDeviceClass('sensor', 'aqi', 'air_quality') || findFirstByDeviceClass('sensor', 'pm25', 'air');
            const powStr = (p && p.state != null && !isNaN(parseFloat(p.state))) ? `${parseFloat(p.state).toFixed(0)} ${p.unit_of_measurement || 'W'}` : '-';
            const airStr = (a && a.state != null && !isNaN(parseFloat(a.state))) ? `${a.device_class === 'aqi' ? 'AQI ' : ''}${parseFloat(a.state).toFixed(0)}${a.unit_of_measurement && a.device_class !== 'aqi' ? ' ' + a.unit_of_measurement : ''}` : '-';
            document.getElementById('dash-temp').textContent = tempStr;
            document.getElementById('dash-power').textContent = powStr;
            document.getElementById('dash-air').textContent = airStr;
            if (priceSnapshot) updatePriceStatus(priceSnapshot);
        }

        async function refreshHaServices() {
            try {
                const r = await fetch(`${API}/api/ha/services`);
                if (!r.ok) return;
                const arr = await r.json();
                // Normalize to { domain: { service_name: { fields: ... } } }
                haServices = {};
                if (Array.isArray(arr)) {
                    for (const block of arr) {
                        if (block && block.domain) haServices[block.domain] = block.services || {};
                    }
                }
            } catch (_) { /* not critical — capabilities table covers the common case */ }
        }

        async function refreshHaConfig() {
            try {
                const r = await fetch(`${API}/api/ha/config`);
                if (!r.ok) return;
                haConfig = await r.json();
            } catch (_) {}
        }

        async function refreshHaStates() {
            try {
                const r = await fetch(`${API}/api/ha/states`);
                if (!r.ok) {
                    const errBody = await r.text().catch(() => '');
                    throw new Error('status=' + r.status + (errBody ? ' — ' + errBody.slice(0, 120) : ''));
                }
                const arr = await r.json();
                haStates = {};
                for (const e of arr) haStates[e.entity_id] = e;
                buildEntityCatalog();
                statusDot.className = 'dot-status ok';
                const instance = (haConfig && haConfig.location_name) ? haConfig.location_name : 'HA';
                statusText.textContent = `SmartNode + ${instance} connected (${entityCatalog.length} entities)`;
                setText(smartnodeStatus, 'Online');
                setText(haStatus, `${instance} connected`);
                setText(entityCount, `${entityCatalog.length} entities loaded`);
                setDataBadge(liveDataBadge, 'Live Home Assistant data', 'ok');
                updateDashboard();
                renderQuickChips();
            } catch (e) {
                statusDot.className = 'dot-status ko';
                const detail = e && e.message ? `: ${e.message}` : '';
                const fileHint = (location.protocol === 'file:')
                    ? ' — page opened via file://; if SmartNode is running, try serving this folder over a local HTTP server (e.g. `python -m http.server 8000`) and reload from http://localhost:8000/'
                    : ' — check SmartNode is running, HA_URL points to the right Home Assistant, and TOKEN_HA is valid.';
                statusText.textContent = `Home Assistant unreachable${detail}${fileHint}`;
                setText(smartnodeStatus, 'Check SmartNode');
                setText(haStatus, 'Disconnected');
                setText(entityCount, detail ? detail.slice(0, 120) : 'No entities loaded');
                setDataBadge(liveDataBadge, 'Home Assistant disconnected', 'warn');
                console.warn('refreshHaStates failed:', e);
            }
        }

        function renderCommandChip(command, label) {
            return `<button type="button" class="chip" data-q="${escapeHtml(command)}">${escapeHtml(label || command)}</button>`;
        }

        function renderQuickGroup(title, items) {
            const buttons = items.filter(Boolean).map(item => renderCommandChip(item.q, item.label)).join('');
            if (!buttons) return '';
            return `<div class="quick-group"><div class="quick-group-title">${escapeHtml(title)}</div><div class="quick-row">${buttons}</div></div>`;
        }

        // Grouped quick actions keep the demo scannable while still deriving devices from HA.
        function renderQuickChips() {
            const container = document.getElementById('quick-chips');
            if (!container) return;

            const explore = [
                { q: 'what can you control?', label: 'What can you control?' },
                { q: "what's the house status", label: 'House status' }
            ];
            const energy = [
                { q: 'what is the energy price', label: 'Energy price' },
                { q: 'nord pool price', label: 'Nord Pool forecast' }
            ];

            const lights = [{ q: 'turn off all lights', label: 'All lights off' }];
            const lightEntities = entityCatalog
                .filter(e => e.domain === 'light' && e.actionable)
                .sort((a, b) => {
                    const preferred = ['basement', 'bedroom', 'dining', 'garage', 'kitchen', 'hallway', 'living'];
                    const rank = e => {
                        const name = `${e.object_id} ${e.friendly_name}`.toLowerCase();
                        const idx = preferred.findIndex(token => name.includes(token));
                        return idx >= 0 ? idx : 99;
                    };
                    const ra = rank(a);
                    const rb = rank(b);
                    if (ra !== rb) return ra - rb;
                    return (a.friendly_name || a.entity_id).localeCompare(b.friendly_name || b.entity_id);
                })
                .slice(0, 5);
            for (const e of lightEntities) {
                const friendly = e.friendly_name || e.entity_id;
                lights.push({ q: `turn on ${friendly}`, label: `Turn on ${friendly}` });
            }

            const sceneScript = entityCatalog
                .filter(e => (e.domain === 'scene' || e.domain === 'script') && e.actionable)
                .sort((a, b) => (a.friendly_name || a.entity_id).localeCompare(b.friendly_name || b.entity_id))
                .slice(0, 4)
                .map(e => {
                    const friendly = e.friendly_name || e.entity_id;
                    return { q: `turn on ${friendly}`, label: `${e.domain === 'scene' ? 'Scene' : 'Script'}: ${friendly}` };
                });

            container.innerHTML = [
                renderQuickGroup('Explore', explore),
                renderQuickGroup('Energy', energy),
                renderQuickGroup('Lights', lights),
                renderQuickGroup('Scenes / Scripts', sceneScript)
            ].join('');
        }

        // Initial load order: config → services → states (states triggers dashboard + chip render).
        if (modelHealthRefresh) modelHealthRefresh.addEventListener('click', () => refreshModelHealth(false));
        if (modelHealthLiveRefresh) modelHealthLiveRefresh.addEventListener('click', () => refreshModelHealth(true));
        (async () => {
            await Promise.all([refreshPrices(), refreshHaConfig(), refreshHaServices(), refreshModelHealth()]);
            await refreshHaStates();
        })();
        setInterval(refreshHaStates, 5000);
        setInterval(refreshPrices, 60000);
        setInterval(refreshHaServices, 60000);
        setInterval(refreshHaConfig, 60000);

        // Proactive arm: polls the read-only advisory MAPE-K computes from Nord Pool prices.
        // Hidden when the advisor has nothing to recommend (median price, forecast unavailable, or
        // MAPE-K hasn't run a cycle yet → 204). The "Preheat now" shortcut bumps the living-room
        // setpoint by 1°C via the same /api/temperature endpoint the chat uses.
        const proactiveBadge = document.getElementById('proactive-badge');
        async function refreshProactive() {
            try {
                const r = await fetch(`${API}/api/proactive/status`);
                if (r.status === 204) { proactiveBadge.style.display = 'none'; return; }
                if (!r.ok) { proactiveBadge.style.display = 'none'; return; }
                const a = await r.json();
                if (!a.forecastAvailable || (!a.shouldPreheat && !a.shouldDeferLoad)) {
                    proactiveBadge.style.display = 'none';
                    return;
                }
                const cls = a.shouldPreheat ? 'preheat' : 'defer';
                const icon = a.shouldPreheat ? '🔥' : '⏸️';
                const btn = a.shouldPreheat
                    ? `<button class="preheat-btn" id="preheat-now-btn">Preheat now (+1°C)</button>`
                    : '';
                proactiveBadge.className = `proactive-badge ${cls}`;
                proactiveBadge.innerHTML = `<span class="icon">${icon}</span><span>${a.reason}</span>${btn}`;
                proactiveBadge.style.display = 'flex';
                const btnEl = document.getElementById('preheat-now-btn');
                if (btnEl) btnEl.onclick = preheatNow;
            } catch (e) {
                proactiveBadge.style.display = 'none';
            }
        }
        // Find the best entity to act as a "temperature setpoint" on the current HA instance.
        // Tries (in order): a climate entity with a current_temperature reading, then a number/
        // input_number whose name or device_class smells like a temperature setpoint. Returns null
        // if nothing matches, so the caller can show a clear error rather than silently failing.
        function findTemperatureSetpointEntity() {
            const climate = entityCatalog.find(e => e.domain === 'climate');
            if (climate) return { entity: climate, kind: 'climate' };
            const num = entityCatalog.find(e =>
                (e.domain === 'input_number' || e.domain === 'number') &&
                (
                    (e.device_class && /temperature|thermostat/i.test(e.device_class)) ||
                    /temp|thermostat|setpoint|consigne|chauff/i.test(e.object_id) ||
                    /temp|thermostat|setpoint|consigne|chauff/i.test(e.friendly_name)
                )
            );
            if (num) return { entity: num, kind: 'number' };
            return null;
        }

        // Read the current temperature from any climate or temperature sensor (whichever exists).
        function readCurrentTemperature() {
            const climate = entityCatalog.find(e => e.domain === 'climate' && e.attributes && e.attributes.current_temperature != null);
            if (climate) return parseFloat(climate.attributes.current_temperature);
            const sensor = findFirstByDeviceClass('sensor', 'temperature', 'temp');
            if (sensor && sensor.state != null && !isNaN(parseFloat(sensor.state))) return parseFloat(sensor.state);
            return NaN;
        }

        async function preheatNow() {
            const setpoint = findTemperatureSetpointEntity();
            if (!setpoint) {
                addMessage('Preheat unavailable: no <code>climate</code> or temperature <code>input_number</code> found on this Home Assistant instance.');
                return;
            }
            const current = readCurrentTemperature();
            const target = isFinite(current) ? Math.round(current) + 1 : 22;
            const cap = setpoint.entity.capabilities && setpoint.entity.capabilities.set;
            if (!cap) {
                addMessage(`Preheat unavailable: <b>${setpoint.entity.friendly_name}</b> does not expose a setpoint service.`);
                return;
            }
            try {
                const r = await fetch(`${API}/api/call_service`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        domain: setpoint.entity.domain,
                        service: cap.service,
                        data: { entity_id: setpoint.entity.entity_id, [cap.dataKey]: target }
                    })
                });
                if (r.ok) addMessage(`🔥 Proactive preheat: <b>${setpoint.entity.friendly_name}</b> → <b>${target}°C</b> (cheap window now, peak coming).`);
                else addMessage(`Preheat failed: ${await r.text()}`);
            } catch (e) {
                addMessage('Preheat failed: SmartNode unreachable.');
            }
        }
        refreshProactive();
        setInterval(refreshProactive, 30000);

        function inferMessageVariant(html, isUser) {
            if (isUser) return '';
            const raw = String(html || '').toLowerCase();
            if (raw.includes('energy-price-card') || raw.includes('nord pool price') || raw.includes('cheapest price') || raw.includes('most expensive price') || raw.includes('daily average price')) return 'energy';
            if (/\b(error|failed|unavailable|unreachable|couldn\'t|cannot|no entities loaded)\b/.test(raw)) return 'error';
            if (/\b(ok|launched|set to|turned|service call|preheat|schedule)\b/.test(raw)) return 'action';
            return '';
        }

        function inferMessageSource(html, variant) {
            const raw = String(html || '').toLowerCase();
            if (variant === 'energy' || raw.includes('nord pool')) return 'Nord Pool';
            if (variant === 'action') return 'Action executed';
            if (variant === 'error') return 'System';
            if (raw.includes('home assistant') || raw.includes('entities') || raw.includes('house status') || raw.includes('sensor')) return 'Home Assistant';
            return 'SmartNode';
        }

        function addMessage(text, isUser = false, options = {}) {
            const meta = typeof options === 'string' ? { source: options } : (options || {});
            const html = String(text ?? '');
            const variant = meta.variant || inferMessageVariant(html, isUser);
            const msgDiv = document.createElement('div');
            msgDiv.classList.add('message', isUser ? 'from-user' : 'from-assistant');
            if (variant) msgDiv.classList.add(`is-${variant}`);

            const metaDiv = document.createElement('div');
            metaDiv.className = 'message-meta';
            const sourceSpan = document.createElement('span');
            sourceSpan.className = 'message-source';
            sourceSpan.textContent = isUser ? 'You' : (meta.source || inferMessageSource(html, variant));
            const timeSpan = document.createElement('span');
            timeSpan.textContent = formatClock(new Date());
            metaDiv.appendChild(sourceSpan);
            metaDiv.appendChild(timeSpan);

            const bubble = document.createElement('div');
            bubble.className = 'message-bubble';
            if (isUser) bubble.textContent = html;
            else bubble.innerHTML = html.replace(/\n/g, '<br>');

            msgDiv.appendChild(metaDiv);
            msgDiv.appendChild(bubble);
            const _emptyState = document.getElementById('chat-empty-state');
            if (_emptyState) _emptyState.classList.add('hidden');
            chatMessages.appendChild(msgDiv);
            chatMessages.scrollTop = chatMessages.scrollHeight;
        }

        function showTyping() {
            chatMessages.appendChild(typingIndicator);
            typingIndicator.style.display = 'flex';
            chatMessages.scrollTop = chatMessages.scrollHeight;
        }
        function hideTyping() { typingIndicator.style.display = 'none'; }

        // Ollama (via SmartNode proxy) + keyword fallback. 10s client timeout so a slow
        // first-load of the Ollama model doesn't leave the UI stuck waiting forever.
        async function classifyIntent(text) {
            const ctrl = new AbortController();
            const timer = setTimeout(() => ctrl.abort(), 120000); // 120s timeout
            try {
                const r = await fetch(`${API}/api/nlu`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ message: text }),
                    signal: ctrl.signal
                });
                clearTimeout(timer);
                if (r.ok) {
                    const obj = await r.json();
                    if (obj && obj.intent) return { source: 'llm', ...obj };
                }
            } catch (e) {
                console.warn('NLU unavailable, using keyword fallback:', e.message);
            } finally {
                clearTimeout(timer);
            }
            return { source: 'keyword', ...keywordFallback(text) };
        }

        // Convert "7am"/"7pm"/"7h" tokens to a 24h integer. Accepts plain digits too.
        function toHour24(numStr, suffix) {
            let n = parseInt(numStr, 10);
            if (isNaN(n)) return null;
            const s = (suffix || '').toLowerCase();
            if (s === 'pm' && n < 12) n += 12;
            if (s === 'am' && n === 12) n = 0;
            return Math.max(0, Math.min(24, n));
        }

        function keywordFallback(text) {
            const t = text.toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g, '');

            // ---------- 1. optimize_schedule (HIGHEST priority) ----------
            // A car/EV charging request must beat any "cheapest/lowest price" intent that would
            // otherwise hijack the message. We trigger optimize_schedule whenever:
            //   (vehicle keyword AND charge keyword) OR
            //   (charge keyword AND (deadline OR cost-concern keyword OR explicit time window))
            const vehicleKw = /\b(tesla|car|ev|vehicle|voiture|vehicule|auto)\b/.test(t);
            const chargeKw  = /\b(charge|charging|recharge|recharger)\b/.test(t);
            const costKw    = /(cheapest|lowest|off[- ]?peak|economical|save\s+money|optimi[sz]e\s+cost|moins.cher|heures?.creuses?)/.test(t);
            // Window: "between 2am and 7am", "from 2 to 7", "entre 2h et 7h"
            const winMatch  = t.match(/(?:between|from|entre)\s+(\d{1,2})\s*(am|pm|h|:00)?\s*(?:and|to|et|-|–)\s+(\d{1,2})\s*(am|pm|h|:00)?/);
            // Deadline alone: "by 7am", "before 7am", "at 7am", "pour 7h", "avant 6h"
            const dlMatch   = t.match(/(?:by|before|at|until|pour|avant|a|à)\s+(\d{1,2})\s*(am|pm|h|:00)?/);
            const hasWindow = !!winMatch;
            const hasDeadline = !!dlMatch;

            if ((vehicleKw && chargeKw) || (chargeKw && (hasDeadline || hasWindow || costKw))) {
                let startHour = null;
                let deadlineHour = 24;
                if (hasWindow) {
                    startHour    = toHour24(winMatch[1], winMatch[2]);
                    deadlineHour = toHour24(winMatch[3], winMatch[4]);
                    if (deadlineHour <= startHour) deadlineHour = (deadlineHour + 24); // overnight wrap-around → cap to 24
                    deadlineHour = Math.min(24, deadlineHour);
                } else if (hasDeadline) {
                    deadlineHour = toHour24(dlMatch[1], dlMatch[2]);
                }
                // Duration: explicit "for 4 hours" / "pendant 4h" / "4 hours of charge", else default to 4.
                const durMatch = t.match(/(\d+)\s*(?:hours?|h(?:eures?)?)\s*(?:of\s+charge|de\s+charge|pour|de)?/);
                const duration = durMatch ? parseInt(durMatch[1], 10)
                              : (startHour != null ? Math.max(1, deadlineHour - startHour) : 4);
                const bgMatch = t.match(/budget.*?(\d+)|max.*?(\d+)\s*(?:nok|kr|couronne)/);
                const budget  = bgMatch ? parseInt(bgMatch[1] || bgMatch[2], 10) : null;
                const isHeat  = /\b(heat|heater|heating|chauff|radiateur)\b/.test(t);
                const power   = vehicleKw ? 11 : (isHeat ? 2 : 1);
                const target  = vehicleKw ? 'CarCharger' : (isHeat ? 'HeaterActuator' : null);
                return {
                    intent: 'optimize_schedule', target, value: null,
                    duration_hours: duration, deadline_hour: deadlineHour, start_hour: startHour,
                    budget_max: budget, power_kw: power, answer: ''
                };
            }

            // ---------- 2. energy price summary (before generic HA entity matching) ----------
            if (isEnergyPriceSummaryQuery(text)) {
                if (/(cheapest|lowest|moins.cher|heures?.creuses?|best)/.test(t))
                    return { intent: 'price_cheapest', target: null, value: null, answer: '' };
                if (/(most\s+expensive|highest|peak|plus.cher|pire)/.test(t))
                    return { intent: 'price_expensive', target: null, value: null, answer: '' };
                if (/(average|mean|moyen)/.test(t))
                    return { intent: 'price_average', target: null, value: null, answer: '' };
                return { intent: 'price_current', target: null, value: null, answer: '' };
            }

            // ---------- 2. set_temperature (numeric setpoint) ----------
            // Run BEFORE actuate so "set 22" never gets parsed as a turn-on.
            const num = (t.match(/(\d+)/) || [])[1];
            if (/\b(temperature|degrees?|degre|chauff|thermostat|set\s+(?:to\s+)?\d+|preheat)\b/.test(t) && num)
                return { intent: 'set_temperature', target: null, value: parseInt(num, 10), answer: '' };
            if (/\bpreheat\b/.test(t)) {
                const cur = parseFloat(haStates['sensor.showcase_living_room_temperature']?.state);
                const target = isFinite(cur) ? Math.round(cur) + 1 : 22;
                return { intent: 'set_temperature', target: null, value: target,
                         answer: `Preheating: living-room setpoint → ${target}°C` };
            }

            // ---------- 3. cover position (numeric open/close/set) ----------
            // "set living blinds to 50%", "open living blinds to 80%", "ferme le volet a 20%".
            const coverPosition = parseCoverPosition(text);
            if (coverPosition != null) {
                const cmd = classifyCommand(text);
                const coverCmd = { ...cmd, preferredDomains: ['cover', ...(cmd.preferredDomains || []).filter(d => d !== 'cover')] };
                const res = resolveEntity(text, { actionableOnly: true, ...coverCmd });
                if (res.match === 'one' && res.entity.domain === 'cover') {
                    return buildSetServiceFromEntity(res.entity, coverPosition, 'keyword');
                }
                if (res.match === 'multiple') {
                    return { intent: 'clarify', candidates: res.candidates, original: text, answer: '' };
                }
                return { intent: 'unknown_entity', original: text, answer: '' };
            }

            // ---------- 3. actuate via the dynamic catalog ----------
            // Detect ON / OFF / mode-activation verbs in EN+FR, then resolve the entity.
            // Crucial: NO default to LivingRoomLight. If nothing matches → unknown_entity.
            const verbOn   = /\b(turn\s+on|switch\s+on|allume|allumer|active|activer|activate|enable|start|launch|run|lance|demarre|lancer|demarrer)\b/.test(t);
            const verbOff  = /\b(turn\s+off|switch\s+off|eteint|eteins|eteindre|coupe|couper|stop|disable|deactivate|kill|arrete|arreter)\b/.test(t);
            const verbOpen = /\b(open|ouvre|ouvrir)\b/.test(t);
            const verbClose = /\b(close|ferme|fermer)\b/.test(t);
            const modeKw   = /\bmode\b/.test(t);
            const bareOn   = /\bon\b/.test(t)  && !chargeKw && !verbOff;
            const bareOff  = /\boff\b/.test(t) && !chargeKw && !verbOn;

            if (verbOn || verbOff || verbOpen || verbClose || modeKw || bareOn || bareOff) {
                const turnOff = (verbOff || verbClose || bareOff) && !(verbOn || verbOpen);
                // "all lights off" / "turn off all the lights" / "eteins toutes les lumieres"
                if (/\b(all|toutes?|tous)\b/.test(t) && /\b(light|lights|lumieres?|lampes?)\b/.test(t)) {
                    return {
                        intent: 'call_service',
                        domain: 'light',
                        service: turnOff ? 'turn_off' : 'turn_on',
                        entity_id: 'all',
                        data: { entity_id: 'all' },
                        answer: `OK — all lights ${turnOff ? 'OFF' : 'ON'}.`
                    };
                }
                const cmd = classifyCommand(text);
                const res = resolveEntity(text, { actionableOnly: true, ...cmd });
                if (res.match === 'none') {
                    return { intent: 'unknown_entity', original: text, answer: '' };
                }
                if (res.match === 'multiple') {
                    return { intent: 'clarify', candidates: res.candidates, original: text, answer: '' };
                }
                const e = res.entity;
                const resolved = serviceForDomain(e.domain, turnOff ? 'off' : 'on');
                if (!resolved) {
                    return { intent: 'cant_turn_off', entity: e, answer: '' };
                }
                const verbLabel = (resolved.service.includes('on') || resolved.service === 'open_cover' || resolved.service === 'open_valve' || resolved.service === 'unlock' || resolved.service === 'start' || resolved.service === 'press') ? 'ON' : 'OFF';
                return {
                    intent: 'call_service',
                    domain: e.domain,
                    service: resolved.service,
                    entity_id: e.entity_id,
                    data: { entity_id: e.entity_id },
                    requiresConfirmation: resolved.requiresConfirmation,
                    answer: `OK — ${e.friendly_name} is now ${verbLabel}.`
                };
            }

            // ---------- 4. set value (numeric setter for input_number / number / fan / cover / climate) ----------
            // "set bedroom thermostat to 21", "fan to 50%", "blinds to 30%". Captured AFTER the
            // dedicated set_temperature shortcut so that one keeps its legacy fast-path.
            const setMatch = t.match(/\b(?:set|put|mets|mettre|regle|regler|change|tourne)\b.*?(\d+)\s*(%|percent|pourcent|degrees?|degres?)?/);
            if (setMatch) {
                const numericVal = parseInt(setMatch[1], 10);
                const cmd = classifyCommand(text);
                const res = resolveEntity(text, { actionableOnly: true, ...cmd });
                if (res.match === 'one' && res.entity.capabilities && res.entity.capabilities.set) {
                    const setCall = buildSetServiceFromEntity(res.entity, numericVal, 'keyword');
                    if (setCall) return setCall;
                }
                if (res.match === 'multiple') {
                    return { intent: 'clarify', candidates: res.candidates, original: text, answer: '' };
                }
            }

            // ---------- 5. capabilities ("what can you control?", "list devices", "liste les appareils") ----------
            if (/\b(what\s+can\s+you\s+(?:control|do)|list\s+(?:devices|controllable|controls|entities)|liste\s+(?:les\s+)?(?:appareils|entites?|elements?|equipements?)|quels?\s+(?:appareils|elements?|equipements?)|capabilities|aide|help)\b/.test(t))
                return { intent: 'capabilities', target: null, value: null, answer: '' };
            if (/\b(which\s+(?:lights?|switches?|devices?)\s+(?:are|sont)\s+on|quelles?\s+(?:lumieres?|lampes?|appareils?)\s+sont\s+allumes?)\b/.test(t))
                return { intent: 'list_on', answer: '' };

            // Specific Nord Pool sensor names stay queryable through the HA entity resolver.
            if (looksLikeSpecificNordPoolSensorQuery(cleanMatchText(text))) {
                const cmd = classifyCommand(text);
                const res = resolveEntity(text, { queryable: true, ...cmd });
                if (res.match === 'one') {
                    return { intent: 'query_state', entity_id: res.entity.entity_id, answer: '' };
                }
                if (res.match === 'multiple') {
                    return { intent: 'clarify', candidates: res.candidates, original: text, answer: '' };
                }
            }

            // ---------- 6. query_state ("what is the bedroom temperature", "is the kitchen light on") ----------
            const queryHints = /\b(status|state|etat|temp|humidity|humidit|power|puissance|air\s*quality|qualite|battery|batterie|level|niveau|is\s+the|are\s+the|is\s+it|quel(?:le)?(?:s)?\s+(?:est|sont)|combien|how\s+much|how\s+many|what(?:'s|\s+is|\s+are)|reading)\b/;
            if (queryHints.test(t)) {
                if (isTemperatureReadQuery(text) && !explicitlyRequestsClimateReading(text)) {
                    const tempSensor = resolveTemperatureSensor(text);
                    if (tempSensor.match === 'one') {
                        return { intent: 'query_state', entity_id: tempSensor.entity.entity_id, answer: '' };
                    }
                    if (tempSensor.match === 'multiple') {
                        return { intent: 'clarify', candidates: tempSensor.candidates, original: text, answer: '' };
                    }
                }
                // Try to resolve a specific entity via the generic resolver (queryable=true allows sensors).
                const cmd = classifyCommand(text);
                const res = resolveEntity(text, { queryable: true, ...cmd });
                if (res.match === 'one') {
                    return { intent: 'query_state', entity_id: res.entity.entity_id, answer: '' };
                }
                if (res.match === 'multiple') {
                    return { intent: 'clarify', candidates: res.candidates, original: text, answer: '' };
                }
                // No specific entity found — house overview.
                return { intent: 'query_state', target: null, value: null, answer: '' };
            }

            // ---------- 7. price ----------
            if (/(price|cost|tariff|prix|tarif|cout|combien)/.test(t)) {
                if (/(cheapest|lowest|moins.cher|heures?.creuses?|best)/.test(t))
                    return { intent: 'price_cheapest',  target: null, value: null, answer: '' };
                if (/(most\s+expensive|highest|peak|plus.cher|pire)/.test(t))
                    return { intent: 'price_expensive', target: null, value: null, answer: '' };
                if (/(average|mean|moyen)/.test(t))
                    return { intent: 'price_average',   target: null, value: null, answer: '' };
                return { intent: 'price_current', target: null, value: null, answer: '' };
            }

            // ---------- 8. greeting ----------
            if (/\b(hello|hi|hey|good\s+(morning|evening|afternoon)|bonjour|salut|coucou)\b/.test(t))
                return { intent: 'greeting', target: null, value: null, answer: '' };

            return { intent: 'unknown', target: null, value: null, answer: '' };
        }

        async function handleIntent(nlu) {
            const { intent, target, value, answer } = nlu;
            switch (intent) {
                case 'greeting':
                    return addMessage(answer || "Hello! What can I do for you? Try <i>what can you control?</i> to see your devices.");
                case 'capabilities':
                    return addMessage(answer || renderCapabilities());
                case 'list_on':
                    return addMessage(renderListOn());
                case 'smalltalk':
                    return addMessage(answer || "Everything's running — the MAPE-K loop is cycling smoothly.");
                case 'out_of_scope':
                    return addMessage(answer || "Sorry, I'm limited to your smart home and energy.");
                case 'query_state': {
                    if (nlu.entity_id && haStates[nlu.entity_id]) {
                        const e = haStates[nlu.entity_id];
                        const attr = e.attributes || {};
                        const friendly = attr.friendly_name || nlu.entity_id;
                        const unit = attr.unit_of_measurement ? ` ${attr.unit_of_measurement}` : '';
                        // For climate, surface current vs target temperature
                        if (nlu.entity_id.startsWith('climate.') && attr.current_temperature != null) {
                            const target = attr.temperature != null ? ` (target ${attr.temperature}°)` : '';
                            return addMessage(`<b>${friendly}</b>: ${attr.current_temperature}°${target}, mode <b>${e.state}</b>.`);
                        }
                        return addMessage((answer || `<b>${friendly}</b>: `) + `<b>${e.state}${unit}</b>.`);
                    }
                    return addMessage(renderHouseStatus(answer));
                }
                case 'set_temperature':
                case 'set_target_temp': {
                    if (value == null) return addMessage("What temperature? (e.g. 'set the temperature to 21 degrees')");
                    // Generic-first: discover a climate entity (or input_number temperature) on
                    // the current HA instance and call its native setpoint service. This works
                    // on any Home Assistant install. Only if no such entity exists do we fall
                    // back to the legacy ruleless URI (showcase MAPE-K binding).
                    const setpoint = findTemperatureSetpointEntity();
                    if (setpoint && setpoint.entity.capabilities && setpoint.entity.capabilities.set) {
                        const cap = setpoint.entity.capabilities.set;
                        try {
                            const r = await fetch(`${API}/api/call_service`, {
                                method: 'POST',
                                headers: { 'Content-Type': 'application/json' },
                                body: JSON.stringify({
                                    domain: setpoint.entity.domain,
                                    service: cap.service,
                                    data: { entity_id: setpoint.entity.entity_id, [cap.dataKey]: value }
                                })
                            });
                            if (r.ok) {
                                addMessage((answer || `OK — <b>${setpoint.entity.friendly_name}</b> set to <b>${value}°C</b>`) + ` <span style="color:var(--text-muted)">(${setpoint.entity.domain}.${cap.service})</span>`);
                                setTimeout(refreshHaStates, 1000);
                            } else {
                                addMessage(`HA service error: ${await r.text()}`);
                            }
                        } catch (e) { addMessage("SmartNode unreachable."); }
                        return;
                    }
                    // Legacy fallback — only relevant on the showcase HA where the ruleless URI
                    // is wired to input_number.showcase_temperature in Factory.cs.
                    try {
                        const r = await fetch(`${API}/api/actuate`, {
                            method: 'POST',
                            headers: { 'Content-Type': 'application/json' },
                            body: JSON.stringify({
                                uri: 'http://www.semanticweb.org/rayan/ontologies/2025/ha/LivingRoomTemperatureInput',
                                state: value
                            })
                        });
                        if (r.ok) {
                            addMessage((answer || `OK — temperature set to <b>${value}°C</b>`) + ` <span style="color:var(--text-muted)">(legacy ruleless binding — no climate/input_number found)</span>`);
                            refreshHaStates();
                        } else {
                            addMessage(`No climate or input_number temperature entity found, and legacy ruleless binding failed: ${await r.text()}`);
                        }
                    } catch (e) { addMessage("SmartNode unreachable."); }
                    return;
                }
                case 'actuate': {
                    if (!target) return addMessage("Which entity should I control? (living room, kitchen, hallway, purifier)");
                    const uri = `http://www.semanticweb.org/rayan/ontologies/2025/ha/${target}`;
                    const state = value ? 1 : 0;
                    try {
                        const r = await fetch(`${API}/api/actuate`, {
                            method: 'POST',
                            headers: { 'Content-Type': 'application/json' },
                            body: JSON.stringify({ uri, state })
                        });
                        if (r.ok) {
                            addMessage((answer || `OK — ${target} → ${value ? 'ON' : 'OFF'}`) + ` <span style="color:var(--text-muted)">(via ruleless → HA)</span>`);
                            refreshHaStates();
                        } else {
                            addMessage(`SmartNode error: ${await r.text()}`);
                        }
                    } catch (e) { addMessage("SmartNode unreachable."); }
                    return;
                }
                case 'call_service': {
                    if (!nlu.domain || !nlu.service) return addMessage("I'm not sure which action to run.");
                    try {
                        const payloadData = nlu.data || {};
                        if (nlu.entity_id && !payloadData.entity_id) {
                            payloadData.entity_id = nlu.entity_id;
                        }
                        const r = await fetch(`${API}/api/call_service`, {
                            method: 'POST',
                            headers: { 'Content-Type': 'application/json' },
                            body: JSON.stringify({
                                domain: nlu.domain,
                                service: nlu.service,
                                data: payloadData
                            })
                        });
                        if (r.ok) {
                            const coverMessage = formatCoverServiceMessage(nlu, payloadData);
                            addMessage(coverMessage || ((answer || `OK — running ${nlu.domain}.${nlu.service}`) + ` <span style="color:var(--text-muted)">(dynamic dispatch → HA)</span>`));
                            setTimeout(refreshHaStates, 1000);
                        } else {
                            addMessage(`Service call error: ${await r.text()}`);
                        }
                    } catch (e) { addMessage("SmartNode unreachable."); }
                    return;
                }
                case 'price_current': {
                    try {
                        const snapshot = await fetchPriceSnapshot();
                        return addMessage(renderNordPoolPriceSummary(snapshot));
                    } catch (e) {
                        return addMessage(renderPriceUnavailable(e));
                    }
                }
                case 'price_cheapest': {
                    try {
                        const snapshot = await fetchPriceSnapshot();
                        const prices = normalizePriceList(snapshot.prices);
                        const unit = String(snapshot.unit || '').trim() || 'NOK/kWh';
                        const m = numberOrNull(snapshot.lowest) ?? (prices.length ? Math.min(...prices) : null);
                        if (m == null) return addMessage(renderNordPoolPriceSummary(snapshot));
                        const idx = prices.findIndex(p => Math.abs(p - m) < 0.0005);
                        const when = idx >= 0 ? ` in <b>${idx}h</b>` : '';
                        return addMessage(`Cheapest price${when}: <b>${escapeHtml(formatPriceAmount(m, unit))}</b>.`);
                    } catch (e) {
                        return addMessage(renderPriceUnavailable(e));
                    }
                }
                case 'price_expensive': {
                    try {
                        const snapshot = await fetchPriceSnapshot();
                        const prices = normalizePriceList(snapshot.prices);
                        const unit = String(snapshot.unit || '').trim() || 'NOK/kWh';
                        const m = numberOrNull(snapshot.highest) ?? (prices.length ? Math.max(...prices) : null);
                        if (m == null) return addMessage(renderNordPoolPriceSummary(snapshot));
                        const idx = prices.findIndex(p => Math.abs(p - m) < 0.0005);
                        const when = idx >= 0 ? ` in <b>${idx}h</b>` : '';
                        return addMessage(`Most expensive price${when}: <b>${escapeHtml(formatPriceAmount(m, unit))}</b>.`);
                    } catch (e) {
                        return addMessage(renderPriceUnavailable(e));
                    }
                }
                case 'price_average': {
                    try {
                        const snapshot = await fetchPriceSnapshot();
                        const prices = normalizePriceList(snapshot.prices);
                        const unit = String(snapshot.unit || '').trim() || 'NOK/kWh';
                        const avg = numberOrNull(snapshot.dailyAverage) ??
                            (prices.length ? prices.reduce((a, b) => a + b, 0) / prices.length : null);
                        if (avg == null) return addMessage(renderNordPoolPriceSummary(snapshot));
                        return addMessage(`Daily average price: <b>${escapeHtml(formatPriceAmount(avg, unit))}</b>.`);
                    } catch (e) {
                        return addMessage(renderPriceUnavailable(e));
                    }
                }
                case 'optimize_schedule': {
                    const payload = {
                        duration_hours: nlu.duration_hours ?? 4,
                        deadline_hour: nlu.deadline_hour ?? 24,
                        start_hour:    nlu.start_hour ?? null,
                        budget_max:    nlu.budget_max ?? null,
                        power_kw:      nlu.power_kw ?? 1,
                        target:        nlu.target ?? null
                    };
                    let plan = null;
                    try {
                        const r = await fetch(`${API}/api/optimize`, {
                            method: 'POST',
                            headers: { 'Content-Type': 'application/json' },
                            body: JSON.stringify(payload)
                        });
                        if (!r.ok) {
                            addMessage(`Optimization error (HTTP ${r.status}): ${await r.text()}`);
                            return;
                        }
                        plan = await r.json();
                    } catch (e) {
                        addMessage(`SmartNode unreachable (fetch failed): ${e && e.message ? e.message : e}`);
                        return;
                    }
                    try {
                        if (plan.forecastAvailable === false || plan.optimized === false) {
                            const reason = plan.reason || 'Future Nord Pool price forecast unavailable.';
                            addMessage(
                                `<b>Cannot optimize cheapest hours.</b><br>` +
                                `${reason}<br>` +
                                `<span style="color:var(--text-muted);font-size:0.8rem">Source: ${plan.priceSource || 'homeassistant:nordpool.get_prices_for_date'}</span>`
                            );
                        } else {
                            addMessage(renderSchedule(plan, answer));
                        }
                    } catch (e) {
                        console.error('renderSchedule failed', e, plan);
                        addMessage(`Render error: ${e && e.message ? e.message : e} (see browser console for details)`);
                    }
                    return;
                }

                case 'clarify': {
                    const cands = filterClarificationCandidates(nlu.candidates || [], null, norm(nlu.original || '')).slice(0, 6);
                    if (!cands.length) return addMessage(`I couldn't disambiguate "<i>${nlu.original || ''}</i>".`);
                    // Pre-build a "turn on/off X" follow-up so clicking a chip resolves cleanly.
                    const isOff = /\b(off|eteint|eteins|stop|disable|coupe|ferme|close)\b/i.test(norm(nlu.original || ''));
                    const verb = isOff ? 'turn off' : 'turn on';
                    const chips = cands.map(c =>
                        `<span class="chip clarify-chip" data-q="${verb} ${(c.friendly_name || c.entity_id).replace(/"/g, '&quot;')}">` +
                        `${c.friendly_name} <span style="color:var(--text-muted);font-size:0.7rem">(${c.domain})</span></span>`
                    ).join(' ');
                    return addMessage(`I matched several items for "<i>${nlu.original}</i>". Pick one:<br><div style="margin-top:6px;display:flex;flex-wrap:wrap;gap:6px">${chips}</div>`);
                }
                case 'unknown_entity': {
                    // Suggest tier-1 + tier-2 controllables; if the catalog is empty, point at HA setup.
                    const known = entityCatalog
                        .filter(e => e.actionable && (DOMAIN_TIERS[e.domain] || 9) <= 2)
                        .slice(0, 8)
                        .map(e => `<span class="chip clarify-chip" data-q="turn on ${(e.friendly_name || e.entity_id).replace(/"/g, '&quot;')}">${e.friendly_name}</span>`).join(' ');
                    return addMessage(
                        `I couldn't find a smart-home item matching "<i>${nlu.original}</i>".<br>` +
                        (known
                            ? `Try one of these (or type <i>what can you control?</i>):<br><div style="margin-top:6px;display:flex;flex-wrap:wrap;gap:6px">${known}</div>`
                            : `(No controllable entities loaded yet — check SmartNode is connected to Home Assistant via HA_URL/TOKEN_HA.)`)
                    );
                }
                case 'cant_turn_off': {
                    return addMessage(
                        `<b>${nlu.entity.friendly_name}</b> is a <code>${nlu.entity.domain}</code> — those are one-shot triggers and can't be turned off. ` +
                        `Try the matching mode/input_boolean instead.`
                    );
                }
                default: {
                    const known = entityCatalog.filter(e => e.actionable).slice(0, 6)
                        .map(e => `<i>${e.friendly_name}</i>`).join(', ');
                    return addMessage(answer || (
                        `I didn't quite get that. Try: <i>what can you control?</i>, <i>turn on [device]</i>, ` +
                        `<i>set [thermostat] to 21</i>, <i>house status</i>, <i>energy price</i>, ` +
                        `<i>charge the Tesla to 100% by 7am</i>.` +
                        (known ? `<br><span style="color:var(--text-muted);font-size:0.8rem">Detected entities: ${known}</span>` : '')
                    ));
                }
            }
        }

        // Render the dynamic capability list — what the bot can ACTUALLY control on this HA instance,
        // grouped by domain. This is the bedrock of the portable behaviour: any HA install gets a
        // truthful answer to "what can you control?" without code changes.
        function renderCapabilities() {
            if (!entityCatalog.length) {
                return `No entities loaded yet. Check that SmartNode is running and HA_URL/TOKEN_HA point at a reachable Home Assistant instance.`;
            }
            const labels = {
                light: '💡 Lights', switch: '🔌 Switches', input_boolean: '🎚️ Toggles',
                fan: '🌀 Fans', cover: '🪟 Covers / Blinds / Doors', valve: '🚿 Valves',
                climate: '🌡️ Climate / Thermostats', water_heater: '🔥 Water heaters', humidifier: '💧 Humidifiers',
                input_number: '🔢 Number inputs', number: '🔢 Numbers',
                select: '🔽 Selects', input_select: '🔽 Selects',
                media_player: '📺 Media players', vacuum: '🤖 Vacuums',
                lock: '🔒 Locks', alarm_control_panel: '🚨 Alarm panels',
                scene: '🎬 Scenes', script: '▶️ Scripts', automation: '⚙️ Automations',
                button: '🔘 Buttons', input_button: '🔘 Buttons',
                remote: '📡 Remotes', siren: '🚨 Sirens',
                sensor: '📊 Sensors (read-only)', binary_sensor: '🟢 Binary sensors (read-only)',
                weather: '🌤️ Weather (read-only)'
            };
            // Group entities by domain.
            const byDomain = {};
            for (const e of entityCatalog) {
                if (!ACTIONABLE_DOMAINS.has(e.domain) && !QUERYABLE_ONLY_DOMAINS.has(e.domain)) continue;
                (byDomain[e.domain] ||= []).push(e);
            }
            const domainOrder = Object.keys(labels).filter(d => byDomain[d]);
            const remaining = Object.keys(byDomain).filter(d => !labels[d]);
            const all = [...domainOrder, ...remaining];
            const lines = all.map(d => {
                const items = byDomain[d];
                const label = labels[d] || `${d}`;
                const display = items.slice(0, 8).map(e => `<i>${e.friendly_name}</i>`).join(', ');
                const overflow = items.length > 8 ? ` <span style="color:var(--text-muted);font-size:0.75rem">…and ${items.length - 8} more</span>` : '';
                return `<b>${label}</b> (${items.length}): ${display}${overflow}`;
            });
            const total = entityCatalog.length;
            const instance = (haConfig && haConfig.location_name) ? ` from <b>${haConfig.location_name}</b>` : '';
            return `Here is what I can see${instance} (${total} entities total):<br>${lines.join('<br>')}` +
                   `<br><span style="color:var(--text-muted);font-size:0.75rem">Try: <i>turn on [device name]</i>, <i>set [thermostat] to 21</i>, <i>what is the [sensor name]</i>.</span>`;
        }

        // Render entities currently in the "on" state, grouped by domain. Useful sanity-check
        // for a generic HA instance ("which lights are on?").
        function renderListOn() {
            const onEntities = entityCatalog.filter(e =>
                ['light', 'switch', 'input_boolean', 'fan', 'media_player'].includes(e.domain) &&
                (e.state === 'on' || e.state === 'playing' || e.state === 'open')
            );
            if (!onEntities.length) return `Nothing seems to be on right now.`;
            const grouped = {};
            for (const e of onEntities) (grouped[e.domain] ||= []).push(e.friendly_name);
            return `Currently on:<br>` + Object.entries(grouped)
                .map(([d, names]) => `• <b>${d}</b>: ${names.map(n => `<i>${n}</i>`).join(', ')}`).join('<br>');
        }

        // House overview — picks dashboard sensors + lights/switches dynamically.
        function renderHouseStatus(answer) {
            const lines = [];
            const climate = entityCatalog.find(e => e.domain === 'climate' && e.attributes && e.attributes.current_temperature != null);
            if (climate) {
                const target = climate.attributes.temperature != null ? ` (target ${climate.attributes.temperature}°)` : '';
                lines.push(`• <b>${climate.friendly_name}</b>: ${climate.attributes.current_temperature}°${target}`);
            } else {
                const t = findFirstByDeviceClass('sensor', 'temperature', 'temp');
                if (t) lines.push(`• <b>${t.friendly_name}</b>: ${t.state} ${t.unit_of_measurement || '°C'}`);
            }
            const p = findFirstByDeviceClass('sensor', 'power', 'power');
            if (p) lines.push(`• <b>${p.friendly_name}</b>: ${p.state} ${p.unit_of_measurement || 'W'}`);
            const a = findFirstByDeviceClass('sensor', 'aqi', 'air_quality');
            if (a) lines.push(`• <b>${a.friendly_name}</b>: ${a.state}${a.unit_of_measurement ? ' ' + a.unit_of_measurement : ''}`);
            // Up to 6 lights/switches with their state.
            const devices = entityCatalog
                .filter(e => ['light', 'switch'].includes(e.domain))
                .slice(0, 6)
                .map(e => `${e.friendly_name}: <b>${e.state}</b>`);
            if (devices.length) lines.push(`• ${devices.join(', ')}`);
            const header = answer ? answer + '<br>' : `Current house status:<br>`;
            return header + (lines.length ? lines.join('<br>') : '<i>(no displayable entities yet)</i>');
        }

        // Map plan id -> plan for the Execute button.
        const _pendingPlans = new Map();

        // Render a 24h schedule visualization: bar heights scale with hourly price so the
        // expensive vs cheap hours are visible at a glance. Tooltips show price + chosen flag.
        function renderSchedule(plan, answer) {
            const currencyForBars = plan.currency || 'NOK';
            const pricesOnly = plan.schedule.map(s => s.price).filter(p => p != null);
            const maxPrice = pricesOnly.length ? Math.max(...pricesOnly) : 1;
            const minPrice = pricesOnly.length ? Math.min(...pricesOnly) : 0;
            const containerH = 64; // px — total bar area height
            const minBarH = 6;     // px — even cheapest bar stays visible
            // Floor the scaling at minPrice so the cheapest hour gets ~minBarH and the most
            // expensive gets ~containerH. With flat days (small spread) this keeps the bars
            // proportional rather than all squashed at the bottom.
            const cells = plan.schedule.map(s => {
                const noData = (s.price == null);
                const color = noData ? 'rgba(100,116,139,0.15)'              // no forecast for this hour
                            : s.on ? '#22c55e'                                // green: chosen (ON)
                            : !s.before_deadline ? '#334155'                  // dark: after deadline
                            : (s.price > plan.avg_price ? 'rgba(239,68,68,0.55)' : 'rgba(148,163,184,0.35)');
                let barH;
                if (noData) {
                    barH = 4;
                } else if (maxPrice === minPrice) {
                    barH = Math.round(containerH * 0.6);
                } else {
                    const ratio = (s.price - minPrice) / (maxPrice - minPrice); // 0..1
                    barH = Math.max(minBarH, Math.round(minBarH + ratio * (containerH - minBarH)));
                }
                const tip = noData
                    ? `h${s.hour}: no future price`
                    : `h${s.hour}: ${s.price.toFixed(3)} ${currencyForBars}/kWh${s.on ? ' ✓ chosen' : (s.before_deadline ? '' : ' (after deadline)')}`;
                return `<div title="${tip}" `
                     + `style="flex:1;height:${barH}px;background:${color};border-right:1px solid rgba(0,0,0,0.3);"></div>`;
            }).join('');

            const targetLabel = plan.target ? ` for <b>${plan.target}</b>` : '';
            const currency = plan.currency || 'NOK';
            const usingForecast = plan.priceSource ? ` using Nord Pool future prices` : '';
            const requestedH = plan.requested_duration_hours ?? plan.duration_hours;
            const actualH = plan.actual_duration_hours ?? plan.duration_hours;
            const slotCount = plan.slot_count ?? (plan.chosen_slots ? plan.chosen_slots.length : 0);
            const durationLabel = (Math.abs(actualH - requestedH) > 0.01)
                ? `${actualH}h (${slotCount} slots, requested ${requestedH}h)`
                : `${actualH}h (${slotCount} slots)`;
            const windowLabel = (plan.chosen_slots && plan.chosen_slots.length)
                ? `${durationLabel} between ${new Date(plan.chosen_slots[0].start).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })} and ${new Date(plan.chosen_slots[plan.chosen_slots.length-1].end).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}`
                : `${durationLabel} before ${plan.deadline_hour}:00`;
            const header = answer || `Optimized plan${targetLabel}${usingForecast} (${windowLabel}):`;
            const budgetLine = plan.budget_max != null
                ? `<br>Budget ${plan.budget_max} ${currency}: ${plan.within_budget ? '<b style="color:#22c55e">✓ respected</b>' : '<b style="color:#ef4444">✗ exceeded</b>'}`
                : '';

            const planId = 'plan-' + Date.now() + '-' + Math.floor(Math.random() * 1000);
            _pendingPlans.set(planId, plan);
            const execBtn = plan.target
                ? `<br><button class="exec-btn" data-exec-plan="${planId}">▶ Run plan (demo mode: 1h = 1min)</button>`
                : `<br><span style="color:var(--text-muted);font-size:0.75rem">(Specify a target — e.g. "charge the Tesla..." — to enable execution)</span>`;

            // Day-prefix each chosen slot so a window crossing midnight reads naturally
            // (e.g. "Today 17:00 / Tomorrow 04:00") instead of being mistaken for a sort glitch.
            const _today = new Date(); _today.setHours(0,0,0,0);
            const dayPrefix = (iso) => {
                const d = new Date(iso); d.setHours(0,0,0,0);
                const diff = Math.round((d.getTime() - _today.getTime()) / 86400000);
                return diff === 0 ? 'Today' : diff === 1 ? 'Tomorrow' : new Date(iso).toLocaleDateString();
            };
            const slotLines = (plan.chosen_slots && plan.chosen_slots.length)
                ? '<br>' + plan.chosen_slots.map(s => {
                    const start = new Date(s.start).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
                    const end   = new Date(s.end).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
                    return `&nbsp;&nbsp;• ${dayPrefix(s.start)} ${start}–${end} — ${s.price.toFixed(2)} ${currency}/kWh`;
                  }).join('<br>')
                : '';

            // Two baselines exposed by the backend:
            //   baseline_cost_nok        = window-average cost (conservative — "didn't optimize within window")
            //   baseline_worst_cost_nok  = N most-expensive hours in window (upper bound — "worst valid plan")
            // We surface both so the demo isn't bottlenecked by flat-price days, where window-avg savings
            // can be ~1% even though the chosen plan is genuinely the cheapest possible.
            const baselineParts = [];
            if (plan.baseline_cost_nok != null) baselineParts.push(`window-avg ${plan.baseline_cost_nok} ${currency}`);
            if (plan.baseline_worst_cost_nok != null) baselineParts.push(`peak-hours ${plan.baseline_worst_cost_nok} ${currency}`);
            const baselineNote = baselineParts.length ? ` (baseline: ${baselineParts.join(', ')})` : '';

            const worstSavingsLine = (plan.baseline_worst_savings_percent != null)
                ? ` &nbsp;•&nbsp; up to <b style="color:#22c55e">${plan.baseline_worst_savings_percent}%</b> vs charging at peak hours`
                : '';

            return `${header}
                <div style="display:flex;align-items:flex-end;margin:8px 0 4px;height:${containerH}px;border-radius:6px;overflow:hidden;background:rgba(15,23,42,0.06);">${cells}</div>
                <div style="display:flex;justify-content:space-between;font-size:0.7rem;color:var(--text-muted);">
                    <span>0h</span><span>6h</span><span>12h</span><span>18h</span><span>24h</span>
                </div>
                <div style="font-size:0.7rem;color:var(--text-muted);margin-top:2px;">
                    Bar height ∝ price (range ${minPrice.toFixed(2)}–${maxPrice.toFixed(2)} ${currencyForBars}/kWh). Hover for exact values.
                </div>
                <div style="font-size:0.7rem;color:var(--text-muted);margin-top:2px;">
                    Price source: <b>Home Assistant Nord Pool live forecast</b> (area ${plan.area || '?'}, ${plan.raw_slot_count ?? '?'} raw slots → ${plan.hourly_bucket_count ?? '?'} hourly buckets in window). Charger workload is a configured demo actuator (${plan.target || 'CarCharger'}, ${plan.power_kw} kW × ${actualH}h).
                </div>
                <div style="margin-top:8px;font-size:0.85rem;line-height:1.5;">
                    Chosen charging windows:${slotLines}<br>
                    ${actualH}h × ${plan.power_kw} kW → <b>${plan.total_cost_nok} ${currency}</b>${baselineNote}<br>
                    <b style="color:#22c55e">Savings: ${plan.savings_percent}%</b> vs window-avg charge${worstSavingsLine}${budgetLine}
                    ${execBtn}
                </div>`;
        }

        async function executePlan(planId, demoMode = true) {
            const plan = _pendingPlans.get(planId);
            if (!plan || !plan.target) return;
            const hoursOn = plan.schedule.map(s => s.on);
            const targetUri = `http://www.semanticweb.org/rayan/ontologies/2025/ha/${plan.target}`;
            try {
                const r = await fetch(`${API}/api/execute_schedule`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        target: targetUri,
                        target_name: plan.target,
                        hours_on: hoursOn,
                        time_unit_seconds: demoMode ? 60 : 3600
                    })
                });
                if (r.ok) {
                    const res = await r.json();
                    addMessage(`▶ Schedule started (id <b>${res.schedule_id}</b>, ${demoMode ? 'demo mode: 1h = 1min' : 'real time'}). Track it in the banner above.`);
                    refreshSchedules();
                } else {
                    addMessage(`Execution error: ${await r.text()}`);
                }
            } catch (e) { addMessage("SmartNode unreachable."); }
        }

        async function refreshSchedules() {
            try {
                const r = await fetch(`${API}/api/schedules`);
                if (!r.ok) return;
                const { schedules } = await r.json();
                const panel = document.getElementById('schedules-panel');
                if (!schedules.length) { panel.classList.remove('visible'); panel.innerHTML = ''; return; }
                panel.classList.add('visible');
                panel.innerHTML = '<b style="font-size:0.72rem;color:var(--text-muted);letter-spacing:0.05em;">⏱ SCHEDULES</b>'
                    + schedules.slice(0, 4).map(s => {
                        const elapsed = Math.floor((Date.now() - new Date(s.started_at).getTime()) / 1000);
                        const totalSec = 24 * s.time_unit_seconds;
                        const progress = Math.min(100, Math.floor(elapsed / totalSec * 100));
                        const cancelBtn = s.status === 'running'
                            ? ` <button class="cancel-btn" data-cancel-id="${s.id}">✕</button>`
                            : '';
                        return `<div class="schedule-row">
                            <span><b>${s.target}</b> <span class="st-${s.status}">${s.status}</span>
                                ${s.status === 'running' ? ` — schedule progress ${s.current_hour}/24h (${progress}%)` : ''}
                            </span>
                            <span><code style="font-size:0.7rem;color:var(--text-muted)">${s.id}</code>${cancelBtn}</span>
                        </div>`;
                    }).join('');
            } catch (_) {}
        }
        setInterval(refreshSchedules, 3000);
        refreshSchedules();

        async function processMessage(text) {
            try {
                if (isEnergyPriceSummaryQuery(text)) {
                    await handleIntent({ intent: 'price_current', target: null, value: null, answer: '', source: 'energy-price-preflight' });
                    return;
                }
                const nlu = await classifyIntent(text);
                // Guardrail: even when the LLM is the source, verify its entity choice agrees
                // with the local catalog resolver. Overrides hallucinated LivingRoomLight defaults.
                const guarded = guardEntityChoice(nlu, text);
                await handleIntent(guarded);
            } catch (e) {
                console.error(e);
                addMessage(`Internal error: ${e.message}`);
            } finally {
                hideTyping();
            }
        }

        function handleSend() {
            const text = chatInput.value.trim();
            if (!text) return;
            addMessage(text, true);
            chatInput.value = '';
            showTyping();
            processMessage(text);
        }

        sendBtn.addEventListener('click', handleSend);
        chatInput.addEventListener('keypress', (e) => { if (e.key === 'Enter') handleSend(); });

        // Quick-action chips: click fills input + submits.
        document.getElementById('quick-chips').addEventListener('click', (e) => {
            const chip = e.target.closest('.chip');
            if (!chip) return;
            chatInput.value = chip.dataset.q;
            handleSend();
        });

        // Sidebar and guide commands submit the same way as quick actions.
        document.addEventListener('click', (e) => {
            const command = e.target.closest('.command-pill[data-q]');
            if (!command) return;
            chatInput.value = command.dataset.q;
            handleSend();
        });

        // Global delegation: Execute-plan, Cancel-schedule, and Clarify-chip clicks.
        document.addEventListener('click', async (e) => {
            // Clarify chips — re-issue a disambiguated query.
            const clarifyChip = e.target.closest('.clarify-chip[data-q]');
            if (clarifyChip && !clarifyChip.closest('#quick-chips')) {
                chatInput.value = clarifyChip.dataset.q;
                handleSend();
                return;
            }
            const execTarget = e.target.closest('[data-exec-plan]');
            if (execTarget) {
                execTarget.disabled = true;
                execTarget.textContent = 'Launching…';
                await executePlan(execTarget.dataset.execPlan, true);
                execTarget.textContent = '✓ Launched';
                return;
            }
            const cancelTarget = e.target.closest('[data-cancel-id]');
            if (cancelTarget) {
                const id = cancelTarget.dataset.cancelId;
                try {
                    await fetch(`${API}/api/cancel_schedule`, {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({ id })
                    });
                    refreshSchedules();
                } catch (_) {}
            }
        });

// Empty-state prompt cards (added in chat UI refresh)
document.addEventListener('DOMContentLoaded', () => {
    const emptyState = document.getElementById('chat-empty-state');
    const chatInput = document.getElementById('chat-input');
    if (!emptyState || !chatInput) return;

    document.querySelectorAll('.prompt-card').forEach(card => {
        card.addEventListener('click', () => {
            const prompt = card.dataset.prompt;
            if (!prompt) return;
            chatInput.value = prompt;
            chatInput.focus();
            // Submit via the same path Enter takes — find the form or fire keydown.
            const form = chatInput.closest('form');
            if (form) {
                form.requestSubmit();
            } else {
                chatInput.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
            }
        });
    });
});

// Sidebar drawer toggle (added in chat UI refresh)
document.addEventListener('DOMContentLoaded', () => {
    const toggle = document.getElementById('sidebar-toggle');
    const shell = document.querySelector('.app-shell');
    if (!toggle || !shell) return;

    toggle.addEventListener('click', () => {
        const open = shell.classList.toggle('sidebar-open');
        toggle.setAttribute('aria-expanded', String(open));
    });

    // Tap outside the open sidebar closes it (mobile UX expectation).
    document.addEventListener('click', (event) => {
        if (!shell.classList.contains('sidebar-open')) return;
        if (event.target === toggle || toggle.contains(event.target)) return;
        const sidebar = shell.querySelector('.app-sidebar');
        if (sidebar && !sidebar.contains(event.target)) {
            shell.classList.remove('sidebar-open');
            toggle.setAttribute('aria-expanded', 'false');
        }
    });
});
