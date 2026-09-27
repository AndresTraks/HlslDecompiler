hs_5_0
hs_decls
dcl_input_control_point_count 2
dcl_output_control_point_count 2
dcl_tessellator_domain domain_isoline
dcl_tessellator_partitioning partitioning_integer
dcl_tessellator_output_primitive output_line
dcl_globalFlags refactoringAllowed
dcl_immediateConstantBuffer { { 4, 0, 0, 0 }, { 8, 0, 0, 0 } }
hs_fork_phase
dcl_hs_fork_phase_instance_count 2
dcl_input vForkInstanceID
dcl_output_siv o0.x, finalLineDensityTessFactor
dcl_output_siv o1.x, finalLineDetailTessFactor
dcl_temps 1
dcl_indexrange o0 2
mov r0.x, vForkInstanceID.x
mov o[r0.x].x, icb[r0.x].x
ret
