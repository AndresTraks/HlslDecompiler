xps_3_0
config AutoSerialize=false
config AutoResource=false
config PsMaxReg=2
dcl_texcoord0 r0
/*    0.0 */       exec
/*    2   */          rcp r1.x___, r0.w
/*    3   */          mul r1.xy__, r1.xxxx, r0.xyyy
/*    4   */          setTexLOD r0.w
/*    5   */          tfetch2D r1, r1.xyx, tf0
/*    6   */          tfetch2D r2, r0.xyx, tf0, UseComputedLOD=false, UseRegisterLOD=true
/*    7   */          tfetch2D r0, r0.xyx, tf0, UseRegisterLOD=true
/*    0.1 */       alloc colors
/*    1.0 */       exece
/*    8   */          add r0, r2, r0
/*    9   */          add oC0, r0, r1
/*    1.1 */       cnop
