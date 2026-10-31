xps_3_0
config AutoSerialize=false
config AutoResource=false
config PsMaxReg=1
dcl_texcoord0 r0.xy
dcl_texcoord1 r1.xy
/*    0.0 */       exec
/*    2   */          setGradientH r1.xyx
/*    3   */          setGradientV r1.yxx
/*    4   */          tfetch2D r0, r0.xyx, tf0, UseRegisterGradients=true
/*    0.1 */       alloc colors
/*    1.0 */       exece
/*    5   */          max oC0, r0, r0
/*    1.1 */       cnop
