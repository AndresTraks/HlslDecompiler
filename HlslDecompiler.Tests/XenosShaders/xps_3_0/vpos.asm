xps_3_0
config AutoSerialize=false
config AutoResource=false
config PsMaxReg=1
config PsParamGenReg=0
config PsGenScreenXY=true
def c252, 0, 0, 0, 0
def c253, 0, 0, 0, 0
def c254, 0, 0, 0, 0
def c255, -1, 2, 0, 0
/*    0.0 */       alloc colors
/*    0.1 */       exece
/*    1   */          rcpc r1.x___, r0.x
/*    2   */          sges r1.x___, r1.x
/*    3   */          max oC0.xy01, r_abs[0].xyyy, r_abs[0].xyyy
/*    4   */          mad oC0.__z_, r1.xxxx, c255.yyyy, c255.xxxx
