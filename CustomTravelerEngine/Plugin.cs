using MelonLoader;
using HarmonyLib;
using Il2Cppplay.day;
using Il2Cppdata;
using Il2Cppapp.vis;
using Il2Cppapp.vis._Image;
using UnityEngine;
using System;
using System.Text;
using System.Collections.Generic;
using System.Text.Json;

[assembly: MelonInfo(typeof(CustomTravelerEngine.Core), "Custom Traveler Engine", "1.9.0", "MrChamio")]

namespace CustomTravelerEngine
{
    // =========================================================
    // JSON STRUCTURES
    // =========================================================
    public class ConfigDef
    {
        public bool verboseDebug { get; set; } = true;
        public int spawnProbability { get; set; } = 100;
    }

    public class CharacterDef
    {
        // Spawning rules per character
        public string[] acceptedIDs { get; set; } = new string[] { "GENERIC" };
        public string[] acceptedNations { get; set; } = new string[] { "ALL" };
        public string[] blacklistedKeywords { get; set; } = new string[0];
        public string requiredGender { get; set; } = "X"; // "M", "F", "X"

        public string customVoiceClip { get; set; }
        public string texTraveler { get; set; }
        public string texPassport { get; set; }
        public string texDni { get; set; }
        public string texAsylum { get; set; }
        public string texFallback { get; set; }
        
        // Scanner images
        public string texScannerFront { get; set; }
        public string texScannerBack { get; set; }

        public string name { get; set; }
        public string gender { get; set; }
        public string genderForced { get; set; }
        public string face { get; set; }
        public string idNumber { get; set; }
    }

    public class LoadedCharacter
    {
        public CharacterDef def;
        
        public Texture2D texTraveler; 
        
        public Texture2D texPassport;
        public Texture2D texDni;
        public Texture2D texAsylum;
        public Texture2D texFallback;
        public Texture2D texScannerFront;
        public Texture2D texScannerBack;
        public AudioClip voiceClip;
    }

    // =========================================================
    // MAIN CLASS
    // =========================================================
    public class Core : MelonMod
    {
        public static ConfigDef config = new ConfigDef();
        public static List<LoadedCharacter> loadedCharacters = new List<LoadedCharacter>();
        
        public static LoadedCharacter activeCharacterForCtor = null;
        public static LoadedCharacter currentBoothCharacter = null;
        public static readonly System.Random rng = new System.Random();
        private static int contadorTraveler = 0;

        public static AudioClip themeClip = null;

        public static string ModsDir => System.IO.Path.Combine(System.Environment.CurrentDirectory, "Mods");

        public static int NuevoIdDebug() => ++contadorTraveler;

        // JSON Parsing options (allows // comments and trailing commas)
        public static readonly JsonSerializerOptions jsonOpts = new JsonSerializerOptions 
        { 
            ReadCommentHandling = JsonCommentHandling.Skip, 
            AllowTrailingCommas = true 
        };

        public static void Log(string mensaje)
        {
            if (config.verboseDebug) MelonLogger.Msg("[ENGINE DEBUG] " + mensaje);
        }

        public static void LogReject(int id, string motivo)
        {
            if (config.verboseDebug)
            {
                MelonLogger.Msg($"[ENGINE DEBUG] Traveler #{id} -> REJECTED");
                MelonLogger.Msg($"[ENGINE DEBUG] Reason: {motivo}");
            }
        }

        public override void OnInitializeMelon()
        {
            MelonLogger.Msg("==========================================");
            MelonLogger.Msg("Initializing Custom Traveler Engine v1.9");

            CargarConfiguracion();
            CargarTemaPrincipal();
            EscanearCarpetasPersonajes();

            MelonLogger.Msg($"Engine Ready: {loadedCharacters.Count} characters loaded.");
            MelonLogger.Msg("==========================================");
        }

        private void CargarConfiguracion()
        {
            string configPath = System.IO.Path.Combine(ModsDir, "ct.conf");
            if (System.IO.File.Exists(configPath))
            {
                try
                {
                    string json = System.IO.File.ReadAllText(configPath);
                    config = JsonSerializer.Deserialize<ConfigDef>(json, jsonOpts);
                    MelonLogger.Msg("[ENGINE] Global configuration ct.conf loaded.");
                }
                catch (Exception e)
                {
                    MelonLogger.Error($"[ENGINE] Error reading ct.conf: {e.Message}");
                }
            }
            else
            {
                var writeOptions = new JsonSerializerOptions { WriteIndented = true };
                System.IO.File.WriteAllText(configPath, JsonSerializer.Serialize(config, writeOptions));
                MelonLogger.Msg("[ENGINE] File ct.conf generated in the Mods folder.");
            }
        }

        private void CargarTemaPrincipal()
        {
            string themePath = System.IO.Path.Combine(ModsDir, "TemaPrincipal.wav");
            if (System.IO.File.Exists(themePath))
            {
                themeClip = CargarAudioSeguro(themePath, "Theme");
            }
        }

        private void EscanearCarpetasPersonajes()
        {
            string baseDir = System.IO.Path.Combine(ModsDir, "CustomTravelers");
            if (!System.IO.Directory.Exists(baseDir))
            {
                System.IO.Directory.CreateDirectory(baseDir);
                MelonLogger.Warning("[ENGINE] CustomTravelers folder created in Mods. Add your characters inside.");
                return;
            }

            string[] characterDirs = System.IO.Directory.GetDirectories(baseDir);
            foreach (string dir in characterDirs)
            {
                string folderName = new System.IO.DirectoryInfo(dir).Name;
                string jsonPath = System.IO.Path.Combine(dir, folderName + ".json");

                if (System.IO.File.Exists(jsonPath))
                {
                    try
                    {
                        string json = System.IO.File.ReadAllText(jsonPath);
                        CharacterDef def = JsonSerializer.Deserialize<CharacterDef>(json, jsonOpts);
                        LoadedCharacter character = new LoadedCharacter { def = def };

                        string logName = string.IsNullOrEmpty(def.name) ? folderName : def.name;

                        if (!string.IsNullOrEmpty(def.texTraveler))
                            character.texTraveler = CargarTextura(System.IO.Path.Combine(dir, def.texTraveler));

                        if (!string.IsNullOrEmpty(def.texPassport))
                            character.texPassport = CargarTextura(System.IO.Path.Combine(dir, def.texPassport));
                        if (!string.IsNullOrEmpty(def.texDni))
                            character.texDni = CargarTextura(System.IO.Path.Combine(dir, def.texDni));
                        if (!string.IsNullOrEmpty(def.texAsylum))
                            character.texAsylum = CargarTextura(System.IO.Path.Combine(dir, def.texAsylum));
                        if (!string.IsNullOrEmpty(def.texFallback))
                            character.texFallback = CargarTextura(System.IO.Path.Combine(dir, def.texFallback));
                        
                        if (!string.IsNullOrEmpty(def.texScannerFront))
                            character.texScannerFront = CargarTextura(System.IO.Path.Combine(dir, def.texScannerFront));
                        if (!string.IsNullOrEmpty(def.texScannerBack))
                            character.texScannerBack = CargarTextura(System.IO.Path.Combine(dir, def.texScannerBack));

                        if (!string.IsNullOrEmpty(def.customVoiceClip))
                            character.voiceClip = CargarAudioSeguro(System.IO.Path.Combine(dir, def.customVoiceClip), logName + "_voice");

                        loadedCharacters.Add(character);
                        MelonLogger.Msg($"[ENGINE] Character loaded successfully: {logName}");

                        if (character.texTraveler == null)
                            MelonLogger.Msg($"[ENGINE] Note: {logName} has no 'texTraveler'. The game's procedural face will be used.");
                    }
                    catch (Exception e)
                    {
                        MelonLogger.Error($"[ENGINE] Error parsing {folderName}.json: {e.Message}");
                    }
                }
            }
        }

        public static bool EsIDAceptado(string id, string[] acceptedIDs)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (acceptedIDs == null || acceptedIDs.Length == 0) return true;

            foreach (string val in acceptedIDs)
            {
                if (string.IsNullOrEmpty(val)) continue;
                if (val.Equals("ANY", StringComparison.OrdinalIgnoreCase)) return true;
                if (id.StartsWith(val, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        public static bool EsNacionAceptada(string nacionOriginal, string[] acceptedNations)
        {
            if (string.IsNullOrEmpty(nacionOriginal)) return false;
            if (acceptedNations == null || acceptedNations.Length == 0) return true;

            foreach (string val in acceptedNations)
            {
                if (string.IsNullOrEmpty(val)) continue;
                if (val.Equals("ALL", StringComparison.OrdinalIgnoreCase)) return true;
                if (val.Equals(nacionOriginal, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        public static bool EsSexoAceptado(string sexoOriginal, string requiredGender)
        {
            if (string.IsNullOrEmpty(requiredGender)) return true;
            if (requiredGender.Equals("X", StringComparison.OrdinalIgnoreCase)) return true;
            if (requiredGender.Equals("ALL", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.IsNullOrEmpty(sexoOriginal)) return false;

            if (requiredGender.StartsWith("M", StringComparison.OrdinalIgnoreCase) && sexoOriginal.Equals("M", StringComparison.OrdinalIgnoreCase)) return true;
            if (requiredGender.StartsWith("F", StringComparison.OrdinalIgnoreCase) && sexoOriginal.Equals("F", StringComparison.OrdinalIgnoreCase)) return true;

            return false;
        }

        public static bool ContieneListaNegra(string errId, string errGroupId, string specError, string[] blacklist)
        {
            if (blacklist == null || blacklist.Length == 0) return false;

            foreach (string clave in blacklist)
            {
                if (string.IsNullOrEmpty(clave)) continue;
                
                if (!string.IsNullOrEmpty(errId) && errId.IndexOf(clave, StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (!string.IsNullOrEmpty(errGroupId) && errGroupId.IndexOf(clave, StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (!string.IsNullOrEmpty(specError) && specError.IndexOf(clave, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        // =========================================================
        // RESOURCE PROCESSING
        // =========================================================
        private static Texture2D CargarTextura(string rutaAbsoluta)
        {
            if (string.IsNullOrEmpty(rutaAbsoluta) || !System.IO.File.Exists(rutaAbsoluta)) return null;
            Texture2D tex = UnityEngine.Object.Instantiate(Texture2D.whiteTexture);
            try
            {
                ImageConversion.LoadImage(tex, System.IO.File.ReadAllBytes(rutaAbsoluta));
                return tex;
            }
            catch { return null; }
        }

        public static Image GenerarImagenHaxe(Texture2D sourceTex, int targetWidth, int targetHeight)
        {
            if (sourceTex == null || targetWidth <= 0 || targetHeight <= 0) return null;
            sourceTex.filterMode = FilterMode.Point;
            sourceTex.wrapMode = TextureWrapMode.Clamp;

            Image img = new Image(targetWidth, targetHeight, null);
            if (sourceTex.width == targetWidth && sourceTex.height == targetHeight)
            {
                for (int y = 0; y < targetHeight; y++)
                {
                    for (int x = 0; x < targetWidth; x++)
                    {
                        UnityEngine.Color c = sourceTex.GetPixel(x, y);
                        uint haxeColor = Pixel_Impl_.fromRGBA(c.r, c.g, c.b, c.a);
                        img.set_nodirty(x, targetHeight - 1 - y, haxeColor);
                    }
                }
            }
            else
            {
                for (int y = 0; y < targetHeight; y++)
                {
                    for (int x = 0; x < targetWidth; x++)
                    {
                        int origX = Mathf.Clamp((int)(((float)x / targetWidth) * sourceTex.width), 0, sourceTex.width - 1);
                        int origY = Mathf.Clamp((int)(((float)y / targetHeight) * sourceTex.height), 0, sourceTex.height - 1);
                        UnityEngine.Color c = sourceTex.GetPixel(origX, origY);
                        uint haxeColor = Pixel_Impl_.fromRGBA(c.r, c.g, c.b, c.a);
                        img.set_nodirty(x, targetHeight - 1 - y, haxeColor);
                    }
                }
            }
            img.dirty();
            return img;
        }

        public static Image GenerarImagenTransparente(int targetWidth, int targetHeight)
        {
            if (targetWidth <= 0 || targetHeight <= 0) return null;
            Image img = new Image(targetWidth, targetHeight, null);
            uint transparentColor = Pixel_Impl_.fromRGBA(0, 0, 0, 0);
            
            for (int y = 0; y < targetHeight; y++)
            {
                for (int x = 0; x < targetWidth; x++)
                {
                    img.set_nodirty(x, targetHeight - 1 - y, transparentColor);
                }
            }
            img.dirty();
            return img;
        }

        private static AudioClip CargarAudioSeguro(string rutaAbsoluta, string clipName)
        {
            if (string.IsNullOrEmpty(rutaAbsoluta) || !System.IO.File.Exists(rutaAbsoluta)) return null;
            try
            {
                AudioClip clip = CargarWavLocal(rutaAbsoluta);
                if (clip != null)
                {
                    clip.name = clipName;
                    clip.hideFlags = HideFlags.DontSave;
                    return clip;
                }
            }
            catch { }
            return null;
        }

        private static AudioClip CargarWavLocal(string path)
        {
            byte[] fileBytes = System.IO.File.ReadAllBytes(path);
            if (fileBytes.Length < 44 || Encoding.ASCII.GetString(fileBytes, 0, 4) != "RIFF" || Encoding.ASCII.GetString(fileBytes, 8, 4) != "WAVE") return null;

            int channels = 0, sampleRate = 0, bitsPerSample = 0, dataPos = -1, dataSize = 0, pos = 12;

            while (pos + 8 <= fileBytes.Length)
            {
                string chunkId = Encoding.ASCII.GetString(fileBytes, pos, 4);
                int chunkSize = BitConverter.ToInt32(fileBytes, pos + 4);
                pos += 8;
                if (pos + chunkSize > fileBytes.Length) break;

                if (chunkId == "fmt ")
                {
                    channels = BitConverter.ToInt16(fileBytes, pos + 2);
                    sampleRate = BitConverter.ToInt32(fileBytes, pos + 4);
                    bitsPerSample = BitConverter.ToInt16(fileBytes, pos + 14);
                }
                else if (chunkId == "data")
                {
                    dataPos = pos;
                    dataSize = chunkSize;
                    break;
                }
                pos += chunkSize;
                if ((pos & 1) != 0) pos++; 
            }

            if (dataPos == -1 || bitsPerSample == 0 || channels == 0 || sampleRate <= 0) return null;
            if (bitsPerSample != 8 && bitsPerSample != 16) return null;

            int bytesPerSample = bitsPerSample / 8;
            int sampleCount = dataSize / bytesPerSample;
            float[] audioData = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                int offset = dataPos + (i * bytesPerSample);
                if (offset + bytesPerSample > fileBytes.Length) break;
                if (bitsPerSample == 16) audioData[i] = BitConverter.ToInt16(fileBytes, offset) / 32768f;
                else audioData[i] = (fileBytes[offset] - 128) / 128f;
            }

            int totalSamplesPerChannel = sampleCount / channels;
            if (totalSamplesPerChannel <= 0) return null;

            AudioClip clip = AudioClip.Create(System.IO.Path.GetFileNameWithoutExtension(path), totalSamplesPerChannel, channels, sampleRate, false);
            clip.SetData(audioData, 0);
            return clip;
        }
    }

    // =============================================================
    // DYNAMIC CHARACTER REGISTRY
    // =============================================================
    public static class CharacterRegistry
    {
        private const int Max = 32;
        private static Queue<IntPtr> order = new Queue<IntPtr>();
        private static Dictionary<IntPtr, LoadedCharacter> map = new Dictionary<IntPtr, LoadedCharacter>();

        public static void Registrar(Face f, LoadedCharacter character)
        {
            if (f == null || character == null) return;
            if (!map.ContainsKey(f.Pointer)) order.Enqueue(f.Pointer);
            
            map[f.Pointer] = character;
            
            while (order.Count > Max)
            {
                IntPtr old = order.Dequeue();
                map.Remove(old);
            }
        }

        public static LoadedCharacter Obtener(Face f)
        {
            if (f != null && map.TryGetValue(f.Pointer, out var character)) return character;
            return null;
        }
    }

    // =============================================================
    // TRAVELER CONSTRUCTOR PATCH
    // =============================================================
    [HarmonyPatch(typeof(Traveler), nameof(Traveler.__hx_ctor_play_day_Traveler))]
    public class TravelerCtorPatch
    {
        public static void Prefix(TravelerSpec spec, Il2Cppdata.Error error)
        {
            Core.activeCharacterForCtor = null;
            Core.currentBoothCharacter = null;
            int debugId = Core.NuevoIdDebug();

            string tId = spec?.id ?? "NULL";
            string eId = error?.id ?? "NULL";
            string eGroupId = error?.groupId ?? "NULL";
            string sError = "NULL";
            
            string tNation = "NULL";
            string tGender = "NULL";
            bool isSpecial = false;

            if (spec?.vars != null)
            {
                try 
                { 
                    sError = spec.get_error() ?? "NULL"; 
                    isSpecial = spec.vars.get((Il2CppSystem.String)"special") != null;

                    var nObj = spec.vars.get((Il2CppSystem.String)"nation");
                    if (nObj != null) tNation = nObj.ToString();

                    var gObj = spec.vars.get((Il2CppSystem.String)"gender");
                    if (gObj != null) tGender = gObj.ToString();
                } 
                catch { }
            }

            if (Core.config.verboseDebug)
            {
                Core.Log($"\n=== EVALUATING NPC #{debugId} ===");
                Core.Log($"[X-RAY] Traveler ID: {tId}");
                Core.Log($"[X-RAY] Original Nation: {tNation}");
                Core.Log($"[X-RAY] Original Gender: {tGender}");
                Core.Log($"[X-RAY] Error ID: {eId}");
                Core.Log($"[X-RAY] Error Group ID: {eGroupId}");
                Core.Log($"[X-RAY] Internal Spec Error: {sError}");
                Core.Log($"[X-RAY] Is Story NPC: {isSpecial}");
            }

            if (Core.loadedCharacters.Count == 0 || spec == null || spec.vars == null)
            {
                Core.LogReject(debugId, "Engine empty or null specs.");
                return;
            }

            if (isSpecial)
            {
                Core.LogReject(debugId, "This is a mandatory story NPC. Skipping to avoid breaking the story.");
            }

            List<LoadedCharacter> personajesValidos = new List<LoadedCharacter>();
            
            foreach (var personaje in Core.loadedCharacters)
            {
                bool idValido = Core.EsIDAceptado(tId, personaje.def.acceptedIDs);
                bool nacionValida = Core.EsNacionAceptada(tNation, personaje.def.acceptedNations);
                bool sexoValido = Core.EsSexoAceptado(tGender, personaje.def.requiredGender);
                bool sinErroresProhibidos = !Core.ContieneListaNegra(eId, eGroupId, sError, personaje.def.blacklistedKeywords);

                if (idValido && nacionValida && sexoValido && sinErroresProhibidos)
                {
                    personajesValidos.Add(personaje);
                }
            }

            if (personajesValidos.Count == 0)
            {
                Core.LogReject(debugId, "No character in CustomTravelers meets the conditions (ID, Nation, Gender, or Blacklist) to overwrite this NPC.");
                return;
            }

            int tirada = Core.rng.Next(0, 100);
            if (tirada >= Core.config.spawnProbability)
            {
                Core.LogReject(debugId, $"Rejected by global spawn probability ({tirada} >= {Core.config.spawnProbability})");
                return;
            }

            LoadedCharacter elegido = personajesValidos[Core.rng.Next(personajesValidos.Count)];
            Core.activeCharacterForCtor = elegido;
            Core.currentBoothCharacter = elegido;

            string nombreMostrar = string.IsNullOrEmpty(elegido.def.name) ? "Unknown" : elegido.def.name;
            Core.Log($"Verdict NPC #{debugId}: {nombreMostrar} SELECTED and ready for injection!");

            Poner(spec, "name", elegido.def.name);
            Poner(spec, "gender", elegido.def.gender);
            Poner(spec, "genderForced", elegido.def.genderForced);
            Poner(spec, "face", elegido.def.face);
            Poner(spec, "idNumber", elegido.def.idNumber);
        }

        public static void Postfix(Traveler __0)
        {
            if (Core.activeCharacterForCtor == null || __0 == null) return;
            
            CharacterRegistry.Registrar(__0.face, Core.activeCharacterForCtor);
            CharacterRegistry.Registrar(__0.docFace, Core.activeCharacterForCtor);

            // Inyección de la cara de la ventanilla con redimensión en vivo
            if (__0.face != null && __0.face.image != null && Core.activeCharacterForCtor.texTraveler != null)
            {
                int targetW = __0.face.image.width;
                int targetH = __0.face.image.height;
                
                Core.Log($"[IMAGE] Injecting main face at booth. Source image ({Core.activeCharacterForCtor.texTraveler.width}x{Core.activeCharacterForCtor.texTraveler.height}) squashed to requested resolution: {targetW}x{targetH}.");
                
                Image resizedFace = Core.GenerarImagenHaxe(Core.activeCharacterForCtor.texTraveler, targetW, targetH);
                __0.face.image = resizedFace;
                __0.face.headOnlyImage = resizedFace;
            }
            
            Core.activeCharacterForCtor = null; 
        }

        private static void Poner(TravelerSpec spec, string clave, string valor)
        {
            if (string.IsNullOrEmpty(valor)) return; 
            try { spec.vars.set((Il2CppSystem.String)clave, (Il2CppSystem.String)valor); } catch { }
        }
    }

    // =============================================================
    // SCANNER HIJACKER (X-Ray) AND OTHER DOCUMENTS
    // =============================================================
    [HarmonyPatch(typeof(BoothEnv), nameof(BoothEnv.getImage))]
    public class PatchBoothEnvGetImage
    {
        public static void Postfix(string paperId, string factId, double scale, ref Image __result)
        {
            if (Core.currentBoothCharacter == null || __result == null) return;

            // Filtramos exclusivamente los requerimientos de la "Foto" del escáner
            if (paperId == "Photo")
            {
                LoadedCharacter character = Core.currentBoothCharacter;

                if (factId == "HeadFront" && character.texScannerFront != null)
                {
                    Core.Log($"[SCANNER] Game requested {factId}. Original resolution: {__result.width}x{__result.height}. Injecting texScannerFront ({character.texScannerFront.width}x{character.texScannerFront.height}).");
                    __result = Core.GenerarImagenHaxe(character.texScannerFront, __result.width, __result.height);
                }
                else if (factId == "HeadBack" && character.texScannerBack != null)
                {
                    Core.Log($"[SCANNER] Game requested {factId}. Original resolution: {__result.width}x{__result.height}. Injecting texScannerBack ({character.texScannerBack.width}x{character.texScannerBack.height}).");
                    __result = Core.GenerarImagenHaxe(character.texScannerBack, __result.width, __result.height);
                }
            }
        }
    }

    // =============================================================
    // PASSPORT / ID CARD PHOTO REPLACEMENT
    // =============================================================
    [HarmonyPatch(typeof(Face), nameof(Face.getDocImage))]
    public class ReemplazoPasaportePatch
    {
        public static void Postfix(Face __instance, double scale, ref Image __result)
        {
            if (__instance == null || __result == null) return;
            
            LoadedCharacter character = CharacterRegistry.Obtener(__instance);
            if (character == null) return;

            Texture2D texSeleccionada = character.texFallback;
            string tipoDoc = "Unknown/Fallback";

            if (__result.width == 40 && __result.height == 48)
            {
                texSeleccionada = character.texPassport ?? character.texFallback;
                tipoDoc = "Passport (40x48)";
            }
            else if (__result.width == 32 && __result.height == 38)
            {
                texSeleccionada = character.texDni ?? character.texFallback;
                tipoDoc = "ID Card (32x38)";
            }
            else if (__result.width == 60 && __result.height == 72)
            {
                texSeleccionada = character.texAsylum ?? character.texFallback;
                tipoDoc = "Asylum/Diplomatic (60x72)";
            }

            if (texSeleccionada != null)
            {
                try
                {
                    Core.Log($"[IMAGE] Game requested photo for {tipoDoc}. Injecting source image of {texSeleccionada.width}x{texSeleccionada.height} squashed to {__result.width}x{__result.height}.");
                    Image docImage = Core.GenerarImagenHaxe(texSeleccionada, __result.width, __result.height);
                    if (docImage != null)
                    {
                        __result = docImage;
                    }
                }
                catch (Exception e) { MelonLogger.Error($"[ENGINE] Error resizing docImage: {e.Message}"); }
            }
        }
    }

    // =============================================================
    // AUDIO - THEME + CUSTOM VOICE REPLACEMENT
    // =============================================================
    [HarmonyPatch(typeof(AudioSource), nameof(AudioSource.Play), new System.Type[] { })]
    public class PatchAudioSourcePlay
    {
        public static void Prefix(AudioSource __instance)
        {
            if (__instance?.clip == null) return;

            if (Core.themeClip != null && __instance.clip.name == "Theme")
            {
                __instance.clip = Core.themeClip;
                return;
            }

            if (Core.currentBoothCharacter != null && Core.currentBoothCharacter.voiceClip != null && 
                __instance.clip.name.Equals("speech-entrant", StringComparison.OrdinalIgnoreCase))
            {
                __instance.clip = Core.currentBoothCharacter.voiceClip;
            }
        }
    }

    [HarmonyPatch(typeof(AudioSource), nameof(AudioSource.PlayOneShot), new System.Type[] { typeof(AudioClip) })]
    public class PatchAudioSourcePlayOneShot
    {
        public static void Prefix(AudioSource __instance, ref AudioClip clip)
        {
            if (__instance == null || clip == null) return;

            if (Core.currentBoothCharacter != null && Core.currentBoothCharacter.voiceClip != null && 
                clip.name.Equals("speech-entrant", StringComparison.OrdinalIgnoreCase))
            {
                clip = Core.currentBoothCharacter.voiceClip;
            }
        }
    }
}