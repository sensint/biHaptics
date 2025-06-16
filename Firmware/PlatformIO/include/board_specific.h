#pragma once

#ifdef BOARD_TEENSY
#include <EEPROM.h>
#include <HX711.h> // install the HX711 library by Rob Tillaart (https://github.com/RobTillaart/HX711)
#include <elapsedMillis.h>

// Teensy-specific pin definitions
#define SENSOR_LEFT_CLOCK_PIN 19
#define SENSOR_LEFT_DATA_PIN 18
#define SENSOR_RIGHT_CLOCK_PIN 24 // Example pin, please change if needed
#define SENSOR_RIGHT_DATA_PIN 25  // Example pin, please change if needed
#define SPEAKER_LEFT_PIN 6
#define SPEAKER_RIGHT_PIN 10

#elif BOARD_ESP32
#include <EEPROM.h>
#include <HX711.h> // install the HX711 library by Rob Tillaart (https://github.com/RobTillaart/HX711)
#include <driver/dac.h>
// #include <elapsedMillis.h>

// ESP32-specific pin definitions
#define SENSOR_LEFT_CLOCK_PIN 19
#define SENSOR_LEFT_DATA_PIN 18
#define SENSOR_RIGHT_CLOCK_PIN 16 // Example pin, please change if needed
#define SENSOR_RIGHT_DATA_PIN 17  // Example pin, please change if needed
#define DAC_CHANNEL_LEFT DAC_CHANNEL_1 // GPIO 25
#define DAC_CHANNEL_RIGHT DAC_CHANNEL_2 // GPIO 26

// ESP32 audio setup
extern dac_channel_t dac_channel_left;
extern dac_channel_t dac_channel_right;
extern uint8_t current_dac_value_left;
extern uint8_t current_dac_value_right;

#endif

// Common HX711 instance
extern HX711 sensor_left;
extern HX711 sensor_right;

// Common elapsed time types
#ifdef BOARD_ESP32
class elapsedMillis {
private:
    unsigned long ms;
public:
    elapsedMillis(void) { ms = millis(); }
    operator unsigned long () const { return millis() - ms; }
    elapsedMillis & operator = (unsigned long val) { ms = millis() - val; return *this; }
    elapsedMillis & operator += (unsigned long val) { ms -= val; return *this; }
    elapsedMillis & operator -= (unsigned long val) { ms += val; return *this; }
};
class elapsedMicros {
private:
    unsigned long us;
public:
    elapsedMicros(void) { us = micros(); }
    operator unsigned long () const { return micros() - us; }
    elapsedMicros & operator = (unsigned long val) { us = micros() - val; return *this; }
    elapsedMicros & operator += (unsigned long val) { us -= val; return *this; }
    elapsedMicros & operator -= (unsigned long val) { us += val; return *this; }
};
#endif