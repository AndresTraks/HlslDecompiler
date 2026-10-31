xps_3_0
config AutoSerialize=false
config AutoResource=false
config PsMaxReg=2
config PsParamGenReg=1
config PsGenScreenXY=true
def c252, 0, 0, 0, 0
def c253, 0, 0, 0, 0
def c254, 0, 0, 0, 0
def c255, -1, 2, 0, 0
dcl_texcoord0 r0
/*    0.0 */       alloc colors
/*    0.1 */       exece
/*    1   */          mul r2, r0, c0
              +       rcpc r1.x___, r1.x
/*    2   */          sges r1.x___, r1.x
/*    3   */          mad r1.x___, r1.xxxx, c255.yyyy, c255.xxxx
/*    4   */          cndgt oC0, r1.xxxx, r2, r0.wzyx
