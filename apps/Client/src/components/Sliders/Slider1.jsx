import Box from '@mui/material/Box';
import Slider from '@mui/material/Slider';

function Slider1({ text, value, onChange, max, min }) {

  const handleChange = (event, newValue) => {
    if (onChange) {
      onChange(newValue);
    }
  };

  return (
    <Box sx={{ width: '100%' }}>
      <p>{text}</p>
      <Slider 
        value={value || max || 100} 
        min={min || 0}
        max={max || 100}
        aria-label="Default" 
        valueLabelDisplay="auto" 
        onChange={handleChange}
      />
    </Box>
  );
}

export default Slider1;