xps_3_0
config AutoSerialize=false
config AutoResource=false
config PsMaxReg=2
def c252, 0, 0, 0, 0
def c253, 0, 0, 0, 0
def c254, 0, 0, 0, 0
def c255, 1.4285715, 0.7, 0, 0
dcl_texcoord0 r0
/*    0.0 */       alloc colors
/*    0.1 */       exece
/*    1   */          mul r2, r0, c255.xxxx
/*    2   */          frc r1, r_abs[2]
/*    3   */          cndge r1, r2, r1, -r1
/*    4   */          mad oC0, r1, c255.yyyy, r0
