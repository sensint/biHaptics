// #define F_CPU_ACTUAL 60000000 // <-- Commented out

#pragma once

#include <cstdint>
#include <cmath>

namespace defaults {

    // Signal generator settings
    constexpr uint16_t kNumberOfBins = 80;
    constexpr uint32_t kSignalDurationUs = 12500;  // in microseconds
    // constexpr float kSignalFreqencyHz = 80.0f;
    constexpr float kSignalAmp = 1.0f;

    // Sensor settings
    constexpr float kFilterWeight = 0.9f;
    constexpr uint32_t kSensorJitterThreshold = 10;     // increase value if vibration starts resonating too much
    constexpr uint32_t kSendSensorDataMaxDelayMs = 30;  // in milliseconds
    constexpr uint32_t kCalibrationDelayMs = 5000;      // in milliseconds
    constexpr uint16_t kCalibrationWeight = 50;         // in grams - set to value of your known weight

    // Sensor settings (Left - Defaults, will be overwritten from EEPROM)
    constexpr float kSensorLeftScale = 1.0f;
    constexpr uint32_t kSensorLeftMinValue = 0;
    constexpr uint32_t kSensorLeftMaxValue = 10000;

    // Sensor settings (Right - Defaults, will be overwritten from EEPROM)
    constexpr float kSensorRightScale = 1.0f;
    constexpr uint32_t kSensorRightMinValue = 0;
    constexpr uint32_t kSensorRightMaxValue = 10000;

    // EEPROM addresses
    constexpr int kEEPROMSensorLeftScaleAddress = 0;     // holds a 32 bit (4 byte) float
    constexpr int kEEPROMSensorLeftMinValueAddress = 4;  // holds a 32 bit (4 byte) uint32_t
    constexpr int kEEPROMSensorLeftMaxValueAddress = 8;  // holds a 32 bit (4 byte) uint32_t
    constexpr int kEEPROMSensorRightScaleAddress = 12;   // holds a 32 bit (4 byte) float
    constexpr int kEEPROMSensorRightMinValueAddress = 16; // holds a 32 bit (4 byte) uint32_t
    constexpr int kEEPROMSensorRightMaxValueAddress = 20;// holds a 32 bit (4 byte) uint32_t
    constexpr int kEEPROMSize = 24; // Minimum size needed for the addresses above

    // Serial settings
    constexpr int kBaudRate = 115200;
} 