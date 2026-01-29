<div id="top"></div>

<!-- PROJECT SHIELDS -->
<!--
*** I'm using markdown "reference style" links for readability.
*** Reference links are enclosed in brackets [ ] instead of parentheses ( ).
*** See the bottom of this document for the declaration of the reference variables
*** for contributors-url, forks-url, etc. This is an optional, concise syntax you may use.
*** https://www.markdownguide.org/basic-syntax/#reference-style-links
-->
[![Contributors][contributors-shield]][contributors-url]
[![Forks][forks-shield]][forks-url]
[![Stargazers][stars-shield]][stars-url]
[![Issues][issues-shield]][issues-url]
[![MIT License][license-shield]][license-url]

<!-- PROJECT LOGO -->
<br />
<div align="center">
  <a href="https://sensint.mpi-inf.mpg.de/">
    <img src="assets/img/sensint_logo.png" alt="Logo" width="121" height="100">
  </a>

  <h3 align="center">Connected Material Experiences using Bimanual Vibrotactile Crosstalk in Virtual Reality</h3>

  <p align="center">
    <b>We'd love to get your feedback and know if you want to explore this research further.</b>
    <br />
<!--     <br />
    <a href="https://github.com/sensint/biHaptics/issues">Report Bug</a>
    ·
    <a href="https://github.com/sensint/biHaptics/issues">Request Feature</a> -->
  </p>
</div>

## About The Project

![Banner images][banner-image]

Perceiving material properties such as elasticity, flexibility, and torsion is inherently bimanual, as we rely on the relative motion of our hands to form a unified sense of materiality. Yet, most vibrotactile material rendering approaches are limited to a single hand or finger. While prior work has explored bimanual haptic interfaces, most depend on specialized hardware for specific interactions. In this paper, we demonstrate design strategies to support bimanual material exploration through motion-coupled vibrotactile feedback. Our technique introduces variable crosstalk between the controllers' vibration to evoke connectedness, making two unconnected devices feel as though they manipulate a single object. The technique generalizes motion-coupled feedback approaches beyond previous single-point explorations. Through two user studies, we show that this approach (1) significantly enhances perceived connectedness and (2) conveys distinct material qualities such as elasticity and torsion. Finally, we present Dvihastīya, an authoring tool for designing connected bimanual experiences in virtual reality.

In this repository, we present two main contributions linked to the paper:
1. Code for replicating the psychophysics and qualitative studies (as mentioned in the paper).
2. Dvihastīya authoring tool for designing connected bimanual material experiences with crosstalk based vibrotactile feedback

<p align="right">(<a href="#top">back to top</a>)</p>

### Built With

## Getting Started

Download the code from the GitHub website or clone repo using your favorite git-client software or with the following command:

   ```sh
   git clone https://github.com/sensint/biHaptics.git
   ```
- Open the folder 'Unity' in the Unity software.
- Connect the Meta Quest 3 headset with the quest link and enable the quest link communication.
- Run the unity scene.
- Hold the left and right-hand controllers in the corresponding hands.
- Moving one or both hands will trigger vibrotactile pulses in either or both hands depending on the crosstalk level.

## Firmware

All firmware and interaction logic were developed in **Unity 2022.3.34f1**.
This Unity project contains the core algorithms for rendering **bimanual vibrotactile feedback**, as well as the **Dvihastīya authoring tool**.

### User Studies
The same Unity project was used to conduct all user studies. The experimental flow is controlled via a dedicated script attached to a Unity GameObject.

##### Setup Instructions

1. **Attach the Script:** Attach the user-study script (Exp1aFinal.cs for study 1; Exp2_v1.cs for study 2) to a GameObject in the scene.

2. **Enable the GameObject:** Toggle the GameObject **on** in the Unity Inspector to activate the study logic.

3. **Configure Experimental Conditions:**
- A default sequence of experimental conditions is present when the script is attached, but it can be changed as desired.
- This sequence can be modified in the Inspector to match the desired study design.

4. **Assign Controllers:** Assign the following objects in the designated fields:
- `LeftHandController`
- `RightHandController`

5. **Run the Scene:** Start the scene to begin the user study.

6. **Switching between conditions:** Press `space-bar` on the keyboard once will pause the ongoing condition and pressing the second time will move it to the next condition. Pressing `b` on the keyboard goes back to the previous condition.

#### Notes

- No visual feedback was used during the user studies.
- All interactions were conveyed exclusively through vibrotactile feedback.

#### Dvihastīya Authoring Tool

To enable the authoring tool, please use the following steps:
+ For visually rendering the rubber-band, pool-noodle, and braided-wire, [obirope](https://assetstore.unity.com/packages/tools/physics/obi-rope-55579?aid=1011l34eQ) was used.

## Hardware

For rendering the designed vibrotactile feedback for both the hands, we used both the hand controllers of Meta Quest 3 headset. No further modifications to the commercially available hardware are made. The refresh rate of the Quest 3 was set to be 120Hz and 1440 × 1600 resolution per eye. The Quest was connected to a PC (Intel Core i7-9700, 32 GB RAM, NVIDIA RTX 2080 Ti) via Quest Link (serial communication using USB connection) to minimize delays.

## Contributing

Contributions are what make the open source community such an amazing place to learn, inspire, and create. Any contributions you make are **greatly appreciated**.

If you have a suggestion that would make this better, please fork the repo and create a pull request. You can also simply open an issue with the tag "enhancement".
Don't forget to give the project a star! Thanks again!

1. Fork the Project
2. Create your Feature Branch (`git checkout -b feature/AmazingFeature`)
3. Commit your Changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the Branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

<p align="right">(<a href="#top">back to top</a>)</p>


## License

Distributed under the MIT License. See `LICENSE.txt` for more information.

<p align="right">(<a href="#top">back to top</a>)</p>


## Contact

Sensorimotor Interaction Group - [website](https://sensint.mpi-inf.mpg.de/) - [@sensintgroup](https://twitter.com/sensintgroup)

Project Link: [https://github.com/sensint/biHaptics](https://github.com/sensint/biHaptics)

<p align="right">(<a href="#top">back to top</a>)</p>


## Acknowledgments

* Othneil Drew for [Best-README-Template](https://github.com/othneildrew/Best-README-Template)

<p align="right">(<a href="#top">back to top</a>)</p>



<!-- MARKDOWN LINKS & IMAGES -->
<!-- https://www.markdownguide.org/basic-syntax/#reference-style-links -->
[contributors-shield]: https://img.shields.io/github/contributors/sensint/biHaptics.svg?style=for-the-badge
[contributors-url]: https://github.com/sensint/biHaptics/graphs/contributors
[forks-shield]: https://img.shields.io/github/forks/sensint/biHaptics.svg?style=for-the-badge
[forks-url]: https://github.com/sensint/biHaptics/network/members
[stars-shield]: https://img.shields.io/github/stars/sensint/biHaptics.svg?style=for-the-badge
[stars-url]: https://github.com/sensint/biHaptics/stargazers
[issues-shield]: https://img.shields.io/github/issues/sensint/biHaptics.svg?style=for-the-badge
[issues-url]: https://github.com/sensint/biHaptics/issues
[license-shield]: https://img.shields.io/github/license/sensint/biHaptics.svg?style=for-the-badge
[license-url]: https://github.com/sensint/biHaptics/blob/master/LICENSE
[banner-image]: assets/img/Banner_Bimanual.png
