xps_3_0
config AutoSerialize=false
config AutoResource=false
config PsMaxReg=5
dcl_texcoord0 r0
dcl_texcoord1 r1
/*    0.0 */       alloc colors
/*    0.1 */       exec
/*    2   */          mul_sat r4, r0, r1
/*    3   */          add r5, r1, -r0
/*    4   */          min r2, r0, r1
/*    5   */          max r3, r0, r1
/*    6   */          sge r1, r1, r0
/*    7   */          mad r0, r5, c0.xxxx, r0
/*    1.0 */       exece
/*    8   */          add r0, r0, r4
/*    9   */          add r0, r0, r1
/*   10   */          add r0, r0, r3
/*   11   */          add oC0, r0, r2
/*    1.1 */       cnop
