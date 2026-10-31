xps_3_0
config AutoSerialize=false
config AutoResource=false
config PsMaxReg=0
dcl_texcoord0 r0.xy
/*    0.0 */       exec
/*    2   */          getGradients r0, r0.xy, tf0
/*    0.1 */       alloc colors
/*    1.0 */       exece
/*    3   */          max oC0, r0.xzyw, r0.xzyw
/*    1.1 */       cnop
