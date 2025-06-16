#include <Arduino.h>
// #include <Wire.h>

#include "config.h"
#include "board_specific.h"

// uncomment to enable printing information of the augmentation to the serial port
// #define DEBUG_A

// Enums
enum Speaker {LEFT, RIGHT};
enum VibrationMode {INDIVIDUAL, MAX_VALUE, COMBINED};

// Global Variables
HX711 sensor_left;
HX711 sensor_right;
float filtered_sensor_value_left = 0.0f;
float filtered_sensor_value_right = 0.0f;
float last_triggered_sensor_val_left = 0.0f;
float last_triggered_sensor_val_right = 0.0f;
float threshold_to_start_trigger = 100.0f; // Common threshold for now

// Control flow variables
// #ifdef BOARD_ESP32
elapsedMillis send_sensor_data_delay_ms;
elapsedMicros pulse_time_us_left;
// #else
// elapsedMillis send_sensor_data_delay_ms = 0;
elapsedMicros pulse_time_us_right;
// #endif

bool is_vibrating_left = false;
bool is_vibrating_right = false;
uint16_t mapped_bin_id_left = 0;
uint16_t mapped_bin_id_right = 0;
uint16_t last_bin_id_left = 0;
uint16_t last_bin_id_right = 0;
bool augmentation_enabled = false;
bool recording_enabled = false;
VibrationMode Vibration_mode = VibrationMode::COMBINED; // Default Mode

// Sine wave modulation variables
float modulation_phase = 0.0f;
constexpr float DEFAULT_MODULATION_FREQ = 80.0f; // Default 80Hz modulation
float modulation_frequency = DEFAULT_MODULATION_FREQ;
elapsedMicros modulation_timer;

// Noise filtering variables
constexpr float NOISE_THRESHOLD = 5.0f;
constexpr float PWM_CARRIER_FREQ = 100000.0f;

// Board-specific variables
#ifdef BOARD_ESP32
dac_channel_t dac_channel_left = DAC_CHANNEL_LEFT;
dac_channel_t dac_channel_right = DAC_CHANNEL_RIGHT;
uint8_t current_dac_value_left = 0;
uint8_t current_dac_value_right = 0;
#endif

// Settings structs
struct SensorSettings
{   
    // Common settings applied to both for simplicity, but could be separated
    float filter_weight = defaults::kFilterWeight;
    uint32_t send_data_delay = defaults::kSendSensorDataMaxDelayMs;
    uint32_t calibration_delay = defaults::kCalibrationDelayMs;
    uint16_t calibration_weight = defaults::kCalibrationWeight;

    // Specific Settings
    float scale = 1.0f;
    uint32_t min_value = 0; // Default, will be overwritten
    uint32_t max_value = 10000; // Default, will be overwritten
};

struct SignalGeneratorSettings
{
    uint16_t number_of_bins = defaults::kNumberOfBins; // Common for both sensors
    uint32_t duration_us = defaults::kSignalDurationUs;
    float amp = defaults::kSignalAmp;
};

//=========== settings instances ===========
// These instances are used to access the settings in the main code.
SensorSettings sensor_settings_left;
SensorSettings sensor_settings_right;
SignalGeneratorSettings signal_generator_settings; // Common Generator Settings

//=========== Forward Declarations for Helper Functions ===========
inline void SetupSerial() __attribute__((always_inline));
inline void SetupAudio() __attribute__((always_inline));
inline void SetupSensors() __attribute__((always_inline));
void CalibrateSensor(HX711& sensor, SensorSettings& settings, int eeprom_scale_addr);
void CalibrateSensorRange(HX711& sensor, SensorSettings& settings, int eeprom_min_addr, int eeprom_max_addr);
void CalibrateMin(HX711& sensor, SensorSettings& settings, int eeprom_min_addr, int eeprom_max_addr); // Needs max addr for check
void TareSensor(HX711& sensor);
void StartPulse(Speaker speaker);
void StopPulse(Speaker speaker);

void SetupSerial() {
    while (!Serial && millis() < 5000);
    Serial.begin(defaults::kBaudRate);
}

void PlaySineWaveBurst() {
    float modulation_phase = 0.0;
    unsigned long start_time = millis();
    return;

    while (millis() - start_time < 500) {
        float modulation = sin(modulation_phase);
        uint8_t modulated_duty = static_cast<uint8_t>(127 * (0.5f + 0.5f * modulation));
        analogWrite(SPEAKER_LEFT_PIN, modulated_duty);
        analogWrite(SPEAKER_RIGHT_PIN, modulated_duty);
        modulation_phase += (2.0 * PI * 80 / 1000);  // Move the sine wave forward
        if (modulation_phase > 2.0 * PI) {
            modulation_phase -= 2.0 * PI;  // Keep phase within bounds
        }
        delayMicroseconds(1000);  // Maintain smooth updates
    }
    // Turn off the signal after the burst
    analogWrite(SPEAKER_LEFT_PIN, 0);
    analogWrite(SPEAKER_RIGHT_PIN, 0);
}

void SetupAudio() {
#ifdef BOARD_TEENSY
    pinMode(SPEAKER_LEFT_PIN, OUTPUT);
    pinMode(SPEAKER_RIGHT_PIN, OUTPUT);
    delay(50); // time for DAC voltage stable
    analogWriteFrequency(SPEAKER_LEFT_PIN, PWM_CARRIER_FREQ);
    analogWriteFrequency(SPEAKER_RIGHT_PIN, PWM_CARRIER_FREQ);
    delay(2000);
    PlaySineWaveBurst();
#elif defined(BOARD_ESP32)
    dac_output_enable(dac_channel_left);
    dac_output_enable(dac_channel_right);
    dac_output_voltage(dac_channel_left, 200); 
    dac_output_voltage(dac_channel_right, 200);
    delay(500);
    dac_output_voltage(dac_channel_left, 0); // Ensure off
    dac_output_voltage(dac_channel_right, 0); // Ensure off
#endif
}

void SetupSensors() {
#ifdef BOARD_ESP32
    // Initialize EEPROM for ESP32
    if (!EEPROM.begin(defaults::kEEPROMSize)) {
      Serial.println("Failed to initialise EEPROM");
      delay(10000); // Wait indefinitely on failure
    }
#endif
#ifdef DEBUG
    Serial.print(F("HX711 library version: "));
    Serial.println(HX711_LIB_VERSION);
    Serial.println("Setting up Left Sensor...");
#endif
    sensor_left.begin(SENSOR_LEFT_DATA_PIN, SENSOR_LEFT_CLOCK_PIN);
    delay(10);
    EEPROM.get(defaults::kEEPROMSensorLeftScaleAddress, sensor_settings_left.scale);
    EEPROM.get(defaults::kEEPROMSensorLeftMinValueAddress, sensor_settings_left.min_value);
    EEPROM.get(defaults::kEEPROMSensorLeftMaxValueAddress, sensor_settings_left.max_value);
    // Apply defaults if EEPROM values are invalid (e.g., NaN scale, or min >= max)
    if (isnan(sensor_settings_left.scale) || sensor_settings_left.scale == 0.0f) sensor_settings_left.scale = defaults::kSensorLeftScale;
    if (sensor_settings_left.min_value >= sensor_settings_left.max_value || sensor_settings_left.max_value == 0) {
        sensor_settings_left.min_value = defaults::kSensorLeftMinValue;
        sensor_settings_left.max_value = defaults::kSensorLeftMaxValue;
    }
    sensor_left.set_scale(sensor_settings_left.scale);
    sensor_left.tare();
#ifdef DEBUG
    Serial.printf(">>> Left Sensor initial values from EEPROM:\n\t scale=%f\n\t min=%d\n\t max=%d\n",
                  sensor_settings_left.scale,
                  (int)sensor_settings_left.min_value,
                  (int)sensor_settings_left.max_value);
    Serial.println(F("Left Sensor Setup."));
    Serial.println(F("Setting up Right Sensor..."));
#endif
    sensor_right.begin(SENSOR_RIGHT_DATA_PIN, SENSOR_RIGHT_CLOCK_PIN);
    delay(10);
    EEPROM.get(defaults::kEEPROMSensorRightScaleAddress, sensor_settings_right.scale);
    EEPROM.get(defaults::kEEPROMSensorRightMinValueAddress, sensor_settings_right.min_value);
    EEPROM.get(defaults::kEEPROMSensorRightMaxValueAddress, sensor_settings_right.max_value);
    // Apply defaults if EEPROM values are invalid
    if (isnan(sensor_settings_right.scale) || sensor_settings_right.scale == 0.0f) sensor_settings_right.scale = defaults::kSensorRightScale;
    if (sensor_settings_right.min_value >= sensor_settings_right.max_value || sensor_settings_right.max_value == 0) {
        sensor_settings_right.min_value = defaults::kSensorRightMinValue;
        sensor_settings_right.max_value = defaults::kSensorRightMaxValue;
    }
    sensor_right.set_scale(sensor_settings_right.scale);
    // sensor_right.set_scale(60.827965);
    sensor_right.tare();
#ifdef DEBUG
    Serial.printf(">>> Right Sensor initial values from EEPROM:\n\t scale=%f\n\t min=%d\n\t max=%d\n",
                  sensor_settings_right.scale,
                  (int)sensor_settings_right.min_value,
                  (int)sensor_settings_right.max_value);
    Serial.println(F("Right Sensor Setup."));
#endif
}

void CalibrateSensor(HX711& sensor, SensorSettings& settings, int eeprom_scale_addr) {
#ifdef DEBUG
    Serial.printf("Sensor units (before calibration): %f\n", sensor.get_units(10));
    Serial.printf("clear the loadcell from any weight\n");
#endif
    // you have some time to unload the cell
    delay(settings.calibration_delay);
    sensor.tare();
#ifdef DEBUG
    Serial.printf("HX711 units (after tare): %f\n", sensor.get_units(10));
    Serial.println(F("place a calibration weight on the loadcell"));
#endif
    // you have some time to load the cell with the calibration weight
    delay(settings.calibration_delay);
    sensor.calibrate_scale(settings.calibration_weight, 10);
    settings.scale = sensor.get_scale();
    // EEPROM.put(defaults::kEEPROMSensorScaleAddress, sensor_settings.scale);
    EEPROM.put(eeprom_scale_addr, settings.scale);
#ifdef BOARD_ESP32
    EEPROM.commit();
#endif
#ifdef DEBUG
    Serial.printf("HX711 units (after calibration): %f\n", sensor.get_units(10));
    Serial.printf("HX711 scale (after calibration): %f\n", settings.scale);
#endif
}

void CalibrateSensorRange(HX711& sensor, SensorSettings& settings, int eeprom_min_addr, int eeprom_max_addr) {
#ifdef DEBUG
    Serial.printf("HX711 units (before range calibration): %f\n", sensor.get_units(10));
    Serial.println("clear the loadcell from any weight\n");
#endif
    // you have some time to unload the cell
    delay(settings.calibration_delay);
    sensor.tare();
    settings.min_value = sensor.get_units(10); // TO CHECK if it should be: settings.min_value = sensor.read_average(10); // Read raw value for min
#ifdef DEBUG
    Serial.printf("min value (after tare): %i\n", (int)settings.min_value);
    Serial.println("place the max. allowed weight on the loadcell\n");
#endif
    // you have some time to load the cell with the maximum weight/force
    delay(settings.calibration_delay);
    settings.max_value = sensor.get_units(10); // TO CHECK if it should be: settings.max_value = sensor.read_average(10); // Read raw value for max
    if (settings.min_value >= settings.max_value)
    {
        settings.min_value = defaults::kSensorLeftMinValue; // Use a default if range is invalid
        settings.max_value = defaults::kSensorLeftMaxValue;
#ifdef DEBUG
        Serial.println("WARNING: min exceeded max value during range calibration. Using Default Values");
#endif
    }
    EEPROM.put(eeprom_min_addr, settings.min_value);
    EEPROM.put(eeprom_min_addr, 0);
    EEPROM.put(eeprom_max_addr, settings.max_value);
    EEPROM.put(eeprom_max_addr, 5000);
#ifdef BOARD_ESP32
    EEPROM.commit();
#endif
#ifdef DEBUG
    Serial.printf("max. value : %i\n", (int)settings.max_value);
    // Optional: Convert to grams for display if scale is known
    // if (settings.scale != 0.0f) {
    //     Serial.printf("Estimated Range (grams): Min=%.2f, Max=%.2f",
    //                   (settings.min_value - sensor.get_offset()) / settings.scale,
    //                   (settings.max_value - sensor.get_offset()) / settings.scale);
    // }
#endif
}

void CalibrateMin(HX711& sensor, SensorSettings& settings, int eeprom_min_addr, int eeprom_max_addr) {
#ifdef DEBUG
    Serial.printf("HX711 units (before calibration): %f\n", sensor.get_units(10));
    Serial.println("rest your hand on the handle");
#endif
    // you have some time to unload the cell
    delay(settings.calibration_delay);
    sensor.tare();
    settings.min_value = sensor.get_units(10);
    if (settings.min_value >= settings.max_value)
    {
        settings.min_value = 0;
#ifdef DEBUG
        Serial.println("WARNING: min exceeded max value");
#endif
    }
    EEPROM.put(eeprom_min_addr, settings.min_value);
#ifdef BOARD_ESP32
    EEPROM.commit();
#endif
#ifdef DEBUG
    Serial.printf("min value (after tare): %i\n", (int)settings.min_value);
#endif
}

void TareSensor(HX711& sensor) {
#ifdef DEBUG
    Serial.println("Taring sensor...");
#endif
    sensor.tare(10); // Average over 10 readings
#ifdef DEBUG
    Serial.println("Tare complete.");
#endif
}

void StartPulse(Speaker speaker) {
    uint8_t base_duty_cycle = static_cast<uint8_t>(signal_generator_settings.amp * 255);
    if (speaker == Speaker::LEFT){
#ifdef BOARD_TEENSY
        float modulation = sin(modulation_phase);
        uint8_t modulated_duty = static_cast<uint8_t>(base_duty_cycle * (0.5f + 0.5f * modulation));
        analogWrite(SPEAKER_LEFT_PIN, modulated_duty);
#elif defined(BOARD_ESP32)
        current_dac_value_left = base_duty_cycle;
        dac_output_voltage(dac_channel_left, current_dac_value_left);
#endif
        pulse_time_us_left = 0;
        is_vibrating_left = true;
#ifdef DEBUG_A
        Serial.printf(">>> Start Left Pulse (Amp: %.2f, Mod Freq: %.2f Hz, Dur: %lu µs, Duty: %d%%)\n",
                      signal_generator_settings.amp, modulation_frequency, 
                      signal_generator_settings.duration_us, (base_duty_cycle * 100) / 255);
#endif
    } 
    if (speaker == Speaker::RIGHT){ // Right Speaker
#ifdef BOARD_TEENSY
        float modulation = sin(modulation_phase);
        uint8_t modulated_duty = static_cast<uint8_t>(base_duty_cycle * (0.5f + 0.5f * modulation));
        analogWrite(SPEAKER_RIGHT_PIN, modulated_duty);
#elif defined(BOARD_ESP32)
        current_dac_value_right = base_duty_cycle;
        dac_output_voltage(dac_channel_right, current_dac_value_right);
#endif
        pulse_time_us_right = 0;
        is_vibrating_right = true;
#ifdef DEBUG_A
        Serial.printf(">>> Start Right Pulse (Amp: %.2f, Mod Freq: %.2f Hz, Dur: %lu µs, Duty: %d%%)\n",
                      signal_generator_settings.amp, modulation_frequency, 
                      signal_generator_settings.duration_us, (base_duty_cycle * 100) / 255);
#endif
    }
}

void StopPulse(Speaker speaker) {
    if (speaker == Speaker::LEFT) {
#ifdef BOARD_TEENSY
        analogWrite(SPEAKER_LEFT_PIN, 0);
#elif defined(BOARD_ESP32)
        current_dac_value_left = 0;
        dac_output_voltage(dac_channel_left, current_dac_value_left);
#endif
        is_vibrating_left = false;
#ifdef DEBUG_A
        Serial.println(">>> Stop Left Pulse");
#endif
    } 
    if (speaker == Speaker::RIGHT) { // Right Speaker
#ifdef BOARD_TEENSY
        analogWrite(SPEAKER_RIGHT_PIN, 0);
#elif defined(BOARD_ESP32)
        current_dac_value_right = 0;
        dac_output_voltage(dac_channel_right, current_dac_value_right);
#endif
        is_vibrating_right = false;
#ifdef DEBUG_A
        Serial.println(">>> Stop Right Pulse");
#endif
    }
}

void setup() {
    SetupSerial();
#ifdef BOARD_TEENSY
    Serial.println("Board: Teensy 4.1");
#elif BOARD_ESP32
    Serial.println("Board: ESP32");
#endif
#ifdef DEBUG
    Serial.println(F("================== PseudoBend++ v2 =================="));
    Serial.println(F("Dual Load Cell / Dual Speaker Control"));
    Serial.println(F("====================================================="));
    Serial.printf("USAGE:");
    Serial.println(F("	--- Calibration ---"));
    Serial.println(F("	 cl : Calibrate Left Sensor (Scale)"));
    Serial.println(F("	 cr : Calibrate Right Sensor (Scale)"));
    Serial.println(F("	 sl : Calibrate Left Sensor Range (Min/Max)"));
    Serial.println(F("	 sr : Calibrate Right Sensor Range (Min/Max)"));
    Serial.println(F("	 ml : Calibrate Left Sensor Min"));
    Serial.println(F("	 mr : Calibrate Right Sensor Min"));
    Serial.println(F("	 tl : Tare Left Sensor"));
    Serial.println(F("	 tr : Tare Right Sensor"));
    Serial.println(F("	--- Control ---"));
    Serial.println(F("	 a  : Toggle Augmentation On/Off"));
    Serial.println(F("	 r  : Toggle Recording On/Off"));
    Serial.println(F("	 m  : Toggle Vibration Mode (Individual / Max Value)"));
    Serial.println(F("	--- Settings ---"));
    Serial.println(F("	 f<num> : Set Frequency (Hz) (e.g., f150)"));
    Serial.println(F("	 b<num> : Set Number of Bins (e.g., b10)"));
    Serial.println(F("	 d<num> : Set Pulse Duration (us) (e.g., d10000)"));
    Serial.println(F("	 w<num> : Set Calibration Weight (grams) (e.g. w50)")); // Add command for calibration weight
    Serial.println(F("-----------------------------------------------------"));
#endif
    SetupAudio();
    SetupSensors();

#ifdef DEBUG
    Serial.printf(">>> signal generator settings \n\t bins: %d \n\t amp: %.2f \n\t freq: %.2f Hz \n\t dur: %d µs\n",
                (int)signal_generator_settings.number_of_bins,
                signal_generator_settings.amp,
                (int)modulation_frequency,
                (int)signal_generator_settings.duration_us);
    Serial.println(F("=====================================================\n\n"));
#endif
    delay(500);
}

void loop() {
    if (Serial.available() > 0) {
        char command = (char)Serial.read();
        char target = ' '; // For commands needing left/right target

        if (strchr("csmt", command)){
            if (Serial.available() > 0){
                target = (char)Serial.read();
            } else {
                command = ' '; // Invalid command if no target provided.
            }
        }

        switch (command) {
            case 'c': // Calibrate Scale
                if (target == 'l') CalibrateSensor(sensor_left, sensor_settings_left, defaults::kEEPROMSensorLeftScaleAddress);
                else if (target == 'r') CalibrateSensor(sensor_right, sensor_settings_right, defaults::kEEPROMSensorRightScaleAddress);
                else Serial.println("Invalid target for 'c' (use 'cl' or 'cr')");
                break;
            case 's': // Calibrate Range (Min/ Max)
                if (target == 'l') CalibrateSensorRange(sensor_left, sensor_settings_left, defaults::kEEPROMSensorLeftMinValueAddress, defaults::kEEPROMSensorLeftMaxValueAddress);
                else if (target == 'r') CalibrateSensorRange(sensor_right, sensor_settings_right, defaults::kEEPROMSensorRightMinValueAddress, defaults::kEEPROMSensorRightMaxValueAddress);
                else Serial.println("Invalid target for 's' (use 'sl' or 'sr')");
                break;
            case 'm': // Calibrate Min or Toggle Mode
                if (target == 'l') CalibrateMin(sensor_left, sensor_settings_left, defaults::kEEPROMSensorLeftMinValueAddress, defaults::kEEPROMSensorLeftMaxValueAddress);
                else if (target == 'r') CalibrateMin(sensor_right, sensor_settings_right, defaults::kEEPROMSensorRightMinValueAddress, defaults::kEEPROMSensorRightMaxValueAddress);
                else Serial.println("Invalid target for 'm' (use 'ml' or 'mr')");
                break;
            case 't': // Tare
                if (target == 'l') TareSensor(sensor_left);
                else if (target == 'r') TareSensor(sensor_right);
                else Serial.println("Invalid target for 't' (use 'tl' or 'tr')");
                break;
            
            // Non-targeted commands
            case 'a': // Toggle Augmentation
                augmentation_enabled = !augmentation_enabled;
                break;
            case 'r': // Toggle Recording
                recording_enabled = !recording_enabled;
                break;
            case 'M':
                Vibration_mode = VibrationMode::MAX_VALUE;
                Serial.println("Vibration Mode set to: MAX_VALUE");
                break;
             case 'I': // Set Vibration Mode to INDIVIDUAL (using capital I)
                 Vibration_mode = VibrationMode::INDIVIDUAL;
                 Serial.println("Vibration Mode set to: INDIVIDUAL");
                 break;
             case 'C': // Set Vibration Mode to COMBINED (using capital C)
                 Vibration_mode = VibrationMode::COMBINED;
                 Serial.println("Vibration Mode set to: COMBINED");
                break;
            // Settings Commands
            case 'f': // frequency
                if (Serial.available()) {
                    modulation_frequency = Serial.parseFloat();;
#ifdef BOARD_TEENSY
                    analogWriteFrequency(SPEAKER_LEFT_PIN, PWM_CARRIER_FREQ);
                    analogWriteFrequency(SPEAKER_RIGHT_PIN, PWM_CARRIER_FREQ);
#elif defined(BOARD_ESP32)
#endif
#ifdef DEBUG
                    Serial.printf("new frequency: %dHz\n", (int)modulation_frequency);
#endif
                }
                break;
            case 'b':// number of bins
                if (Serial.available()) {
                    signal_generator_settings.number_of_bins = (uint16_t)Serial.parseInt();
#ifdef DEBUG
                    Serial.printf("new number of bins: %d\n", (int)signal_generator_settings.number_of_bins);
#endif
                }
                break;
            case 'd':// pulse duration
                if (Serial.available())
                {
                    signal_generator_settings.duration_us = (uint32_t)Serial.parseInt();
#ifdef DEBUG
                    Serial.printf("new pulse duration: %dus\n", (int)signal_generator_settings.duration_us);
#endif
                }
                break;
        }
        while(Serial.available() > 0) Serial.read();
    }

    // --- Sensor Reading and Processing ---
    // once the load cell is ready to be read, we calculate the current bin
    if (sensor_left.is_ready()) {
        // this will use units, i.e. grams
        auto sensor_value_left = sensor_left.get_units(1);
        // delayMicroseconds(1); // This is crucial to have time for the get_units
        // this will limit the load cell to only one direction in the range of the calibrated values
        sensor_value_left = constrain(sensor_value_left, sensor_settings_left.min_value, sensor_settings_left.max_value); // Comment this to Check for both sided configurations.
        filtered_sensor_value_left = (1.f - sensor_settings_left.filter_weight) * filtered_sensor_value_left + sensor_settings_left.filter_weight * sensor_value_left;

        // calculate the bin id depending on the filtered sensor value
        // (currently linear mapping)
        if (sensor_settings_left.min_value < sensor_settings_left.max_value){
            mapped_bin_id_left = map(filtered_sensor_value_left, sensor_settings_left.min_value, sensor_settings_left.max_value, 0, signal_generator_settings.number_of_bins);
        } else{
            mapped_bin_id_left = 0;
        }
        mapped_bin_id_left = constrain(mapped_bin_id_left, 0, signal_generator_settings.number_of_bins);
    }

    if (sensor_right.is_ready()) {
        auto sensor_value_right = sensor_right.get_units(1);
        // delayMicroseconds(1);
        sensor_value_right = constrain(sensor_value_right, sensor_settings_right.min_value, sensor_settings_right.max_value); // Comment this to Check for both sided configurations.
        filtered_sensor_value_right = (1.f - sensor_settings_right.filter_weight) * filtered_sensor_value_right + sensor_settings_right.filter_weight * sensor_value_right;

        if (sensor_settings_right.min_value < sensor_settings_right.max_value){
            mapped_bin_id_right = map(filtered_sensor_value_right, sensor_settings_right.min_value, sensor_settings_right.max_value, 0, signal_generator_settings.number_of_bins);
        } else{
            mapped_bin_id_right = 0;
        }
        mapped_bin_id_right = constrain(mapped_bin_id_right, 0, signal_generator_settings.number_of_bins);
    }

    // send the filtered value to the Unity application in a fixed update rate
    if (recording_enabled && send_sensor_data_delay_ms > sensor_settings_left.send_data_delay)
    {
        Serial.print((int)filtered_sensor_value_left);
        Serial.print(",");
        Serial.println((int)filtered_sensor_value_right);
        send_sensor_data_delay_ms = 0;
    }

    // --- Augmentation / Vibration Logic ---
    // Static variables for COMBINED mode state tracking
    static uint16_t prev_triggering_bin_id_combined = 0;
    static bool was_vibrating_combined = false;

    // NOTE: If augmentation is disabled, no code below this line will be executed.
    if (!augmentation_enabled) {
        if (is_vibrating_left) StopPulse(Speaker::LEFT);
        if (is_vibrating_right) StopPulse(Speaker::RIGHT);
        was_vibrating_combined = false; // Reset combined state vibration
        return;
    }

    // Calculating Ranges to figure out the dominant sensor:
    float min_grams_l = (sensor_settings_left.min_value - sensor_left.get_offset()) / sensor_settings_left.scale;
    float max_grams_l = (sensor_settings_left.max_value - sensor_left.get_offset()) / sensor_settings_left.scale;
    float range_l = (max_grams_l > min_grams_l) ? (max_grams_l - min_grams_l) : 0.0f;

    float min_grams_r = (sensor_settings_right.min_value - sensor_right.get_offset()) / sensor_settings_right.scale;
    float max_grams_r = (sensor_settings_right.max_value - sensor_right.get_offset()) / sensor_settings_right.scale;
    float range_r = (max_grams_r > min_grams_r) ? (max_grams_r - min_grams_r) : 0.0f;

    // auto dist = std::abs((int)(filtered_sensor_value - last_triggered_sensor_val));
    // if (dist < defaults::kSensorJitterThreshold) {
    //     return;
    // }

    // --- Individual Mode ---
    if (Vibration_mode == VibrationMode::INDIVIDUAL) { 
        // Left Speaker Control
        // filtered_sensor_value > threshold_to_start_trigger this condition would make it vibrate only if the force applied is more than the threshold to vibrate
        if (mapped_bin_id_left != last_bin_id_left && filtered_sensor_value_left > threshold_to_start_trigger){
            if (is_vibrating_left){
                StopPulse(Speaker::LEFT);
                delayMicroseconds(10);
#ifdef DEBUG_A
                Serial.println(F(">>> Stop Left Pulse before it finished"));
#endif
            }
            StartPulse(Speaker::LEFT);
            last_bin_id_left = mapped_bin_id_left;
            last_triggered_sensor_val_left = filtered_sensor_value_left;
        }
        // Right Speaker Control
        if (mapped_bin_id_right != last_bin_id_right && filtered_sensor_value_right > threshold_to_start_trigger){
            if (is_vibrating_right){
                StopPulse(Speaker::RIGHT);
                delayMicroseconds(10);
#ifdef DEBUG_A
                Serial.println(F(">>> Stop Right Pulse before it finished"));
#endif
            }
            StartPulse(Speaker::RIGHT);
            last_bin_id_right = mapped_bin_id_right;
            last_triggered_sensor_val_right = filtered_sensor_value_right;
        }
        was_vibrating_combined = false; // Reset combined state if switched from combined mode
    }
    // --- Max Value Mode ---
    else if (Vibration_mode == VibrationMode::MAX_VALUE){
        float max_val = max(filtered_sensor_value_left, filtered_sensor_value_right);
        uint16_t max_bin_id = 0;
        Speaker target_speaker = Speaker::LEFT;
        uint16_t last_max_bin_id = max(last_bin_id_left, last_bin_id_right); 

        if (filtered_sensor_value_right > filtered_sensor_value_left) {
            target_speaker = Speaker::RIGHT;
            max_bin_id = map(max_val, sensor_settings_right.min_value, sensor_settings_right.max_value, 0, signal_generator_settings.number_of_bins);
        } else {
            target_speaker = Speaker::LEFT;
            max_bin_id = map(max_val, sensor_settings_left.min_value, sensor_settings_left.max_value, 0, signal_generator_settings.number_of_bins);
        }
        max_bin_id = constrain(max_bin_id, 0, signal_generator_settings.number_of_bins);

        if (max_bin_id != last_max_bin_id && max_val > threshold_to_start_trigger) {
            // Stop the non-target speaker if it was vibrating
            if (target_speaker == Speaker::LEFT && is_vibrating_right) StopPulse(Speaker::RIGHT);
            if (target_speaker == Speaker::RIGHT && is_vibrating_left) StopPulse(Speaker::LEFT);

            // Stop and restart target speaker only if needed
            if (is_vibrating_left && target_speaker == Speaker::LEFT) StopPulse(Speaker::LEFT);
            if (is_vibrating_right && target_speaker == Speaker::RIGHT) StopPulse(Speaker::RIGHT);
            delayMicroseconds(10);

            StartPulse(target_speaker);
            // Update last bin IDs - maybe just store the max_bin_id?
            last_bin_id_left = mapped_bin_id_left; // Keep individual last bins tracked
            last_bin_id_right = mapped_bin_id_right;
            // Or maybe update the specific last_triggered_val?
             if (target_speaker == Speaker::LEFT) last_triggered_sensor_val_left = max_val;
             else last_triggered_sensor_val_right = max_val;
        } else if (max_val <= threshold_to_start_trigger) {
             // Stop vibration if max value drops below threshold
             if (is_vibrating_left) StopPulse(Speaker::LEFT);
             if (is_vibrating_right) StopPulse(Speaker::RIGHT);
        }
        was_vibrating_combined = false;
    }
    // --- Combined Mode ---
    else if (Vibration_mode == VibrationMode::COMBINED){
        bool left_active = filtered_sensor_value_left > threshold_to_start_trigger;
        bool right_active = filtered_sensor_value_right > threshold_to_start_trigger;
        bool should_vibrate_combined = left_active || right_active;
        uint16_t current_triggering_bin_id = 0;

        // Only consider a sensor active if it's above threshold and its bin has changed
        if (left_active && mapped_bin_id_left != last_bin_id_left) {
            should_vibrate_combined = true;
            current_triggering_bin_id = mapped_bin_id_left;
        }
        if (right_active && mapped_bin_id_right != last_bin_id_right) {
            should_vibrate_combined = true;
            if (range_r > range_l) {
                current_triggering_bin_id = mapped_bin_id_right;
            }
        }

        // Check for bin change and actuation
        bool trigger_change = (should_vibrate_combined && (current_triggering_bin_id != prev_triggering_bin_id_combined)) || (should_vibrate_combined != was_vibrating_combined);

        if (trigger_change){
            if (should_vibrate_combined){
                // Stop both before starting
                if (is_vibrating_left) StopPulse(Speaker::LEFT);
                if (is_vibrating_right) StopPulse(Speaker::RIGHT);
                delayMicroseconds(100);
                StartPulse(Speaker::LEFT); // Start both
                StartPulse(Speaker::RIGHT);
            } else {
                // Stop both if neither sensor is active
                 if (is_vibrating_left) StopPulse(Speaker::LEFT);
                 if (is_vibrating_right) StopPulse(Speaker::RIGHT);
            }
        }

        // Update state for next iteration
        was_vibrating_combined = should_vibrate_combined;
        prev_triggering_bin_id_combined = current_triggering_bin_id;

        // Update individual bin IDs
        last_bin_id_left = mapped_bin_id_left;
        last_bin_id_right = mapped_bin_id_right;
    }
    
    // --- Pulse Duration Handling ---
    if (is_vibrating_left && pulse_time_us_left >= signal_generator_settings.duration_us) {
        StopPulse(Speaker::LEFT); // stop pulse if duration is exceeded
    }
    if (is_vibrating_right && pulse_time_us_right >= signal_generator_settings.duration_us){
        StopPulse(Speaker::RIGHT);
    }

    // --- Sine Wave Modulation Update ---
    if (is_vibrating_left || is_vibrating_right) {
        // Update modulation phase based on time and frequency
        float delta_time = modulation_timer / 1000000.0f; // Convert to seconds
        modulation_phase += TWO_PI * modulation_frequency * delta_time;
        if (modulation_phase >= TWO_PI) {
            modulation_phase -= TWO_PI;
        }
        modulation_timer = 0; // Reset timer for next update

        // Update PWM duty cycle with new modulation
#ifdef BOARD_TEENSY
        if (is_vibrating_left) {
            float modulation = sin(modulation_phase);
            uint8_t modulated_duty = static_cast<uint8_t>(signal_generator_settings.amp * 255 * (0.5f + 0.5f * modulation));
            analogWrite(SPEAKER_LEFT_PIN, modulated_duty);
        }
        if (is_vibrating_right) {
            float modulation = sin(modulation_phase);
            uint8_t modulated_duty = static_cast<uint8_t>(signal_generator_settings.amp * 255 * (0.5f + 0.5f * modulation));
            analogWrite(SPEAKER_RIGHT_PIN, modulated_duty);
        }
#endif
    }
}
